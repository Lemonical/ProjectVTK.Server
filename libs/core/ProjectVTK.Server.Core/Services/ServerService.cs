using Fleck;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;
using ProjectVTK.Shared.Helpers;
using System.Text.Json;
using Websocket.Client;

namespace ProjectVTK.Server.Core.Services;

public class ServerService
{
    public bool IsCompatibleVersion { get; private set; }
    public const float Version = 1f;

    private readonly ConfigService _configService;
    private readonly CommandHandlerFactory _handlerFactory;
    private readonly ServerSessions _sessions;
    private readonly CommandService _commands;
    private readonly ILogger<ServerService> _logger;
    private WebSocketServer? _server;
    private WebsocketClient? _client;

    // TODO: Change to actual IP
    private const string _masterUrl = "ws://127.0.0.1:6565";
    private bool _hasWarnedDisconnection;

    public ServerService(CommandHandlerFactory handlerFactory, CommandService commands, ConfigService configService, ServerSessions clientService, ILogger<ServerService> logger)
    {
        _handlerFactory = handlerFactory;
        _commands = commands;
        _configService = configService;
        _sessions = clientService;
        _logger = logger;

        ConnectToMaster();
    }

    public void Start(CancellationToken cancellationToken)
    {
        var configs = _configService.Server;
        if (configs == null) return;

        _server = new WebSocketServer($"ws://0.0.0.0:{configs.Network.Port}");

        _logger.LogInformation("Config file loaded with name: {name}, port: {port}, max users: {maxUsers}",
            configs.Metadata.Name, configs.Network.Port, configs.Network.MaxUsers);

        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                var session = new WebSocketSession(socket);
                _sessions.AddSession(session);
                _logger.LogInformation("[{ip,-16}] connected", socket.ConnectionInfo.ClientIpAddress);
            };

            socket.OnClose = () =>
            {
                var session = _sessions.RemoveSession(socket);
                _logger.LogInformation("[{ip,-16}] {username,16} disconnected", socket.ConnectionInfo.ClientIpAddress, session?.Username);
            };

            socket.OnMessage = async payload =>
            {
                var session = _sessions.GetSession(socket);
                if (string.IsNullOrWhiteSpace(payload) || session == null) return;
                _logger.LogDebug("[Master] Received:\n{msg}", payload);
                try
                {
                    var command = JsonSerializer.Deserialize<Command>(payload, JsonHelper.GetSerializerOptions());

                    if (command != default)
                    {
                        var handler = _handlerFactory.GetHandler(command.Protocol);
                        if (handler != null)
                            await handler.HandleAsync(command, session);
                        else
                            await socket.Send(
                                Command.CreateResponse(
                                    command.Id.GetValueOrDefault(), command.Protocol, CommandStatusCode.Failed, "Unknown protocol").AsJson());
                    }
                    else
                        await socket.Send(
                            Command.CreateResponse(
                                command.Id.GetValueOrDefault(), command.Protocol, CommandStatusCode.Failed, "Unknown protocol").AsJson());
                }
                catch (JsonException ex)
                {
                    // TODO: Extract GUID then respond with it
                    _logger.LogError(ex, "Error parsing JSON string: {payload}", payload);
                }
            };
        });

        cancellationToken.WaitHandle.WaitOne();
        Stop();
    }

    public void Stop()
    {
        _server?.Dispose();
        _client?.Dispose();
        _logger.LogInformation("Server stopped");
    }

    private void ConnectToMaster()
    {
        var exitEvent = new ManualResetEvent(false);
        _logger.LogInformation("Connecting to master server");
        _client = new WebsocketClient(new(_masterUrl))
        {
            ReconnectTimeout = null,
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10),
            LostReconnectTimeout = TimeSpan.FromSeconds(10)
        };

        // Subscribe
        _client.DisconnectionHappened.Subscribe(_ =>
        {
            if (_hasWarnedDisconnection) return;

            _hasWarnedDisconnection = true;
            _logger.LogWarning("Disconnected from master server");
        });
        _client.ReconnectionHappened.Subscribe(async _ =>
        {
            _hasWarnedDisconnection = false;
            _logger.LogInformation("Connected to master server");
            var vcResponse = await _commands.SendCommandAsync(_client,
                Command.CreateRequest(
                    new VersionCheckCommandData()
                    { Type = ClientType.Client, Version = Version }
                ));

            IsCompatibleVersion = vcResponse.Status == CommandStatusCode.Success;
            if (!IsCompatibleVersion)
                _logger.LogError("Running version {version}, but it is not compatible!", Version);
            else
                _logger.LogInformation("{message}", ((VersionCheckCommandData)vcResponse.Data!).Message);
        });
        _client.MessageReceived.Subscribe(async payload =>
        {
            if (string.IsNullOrWhiteSpace(payload.Text)) return;
            _logger.LogDebug("[Master] Received:\n{msg}", payload.Text);

            try
            {
                var command = JsonSerializer.Deserialize<Command>(payload.Text, JsonHelper.GetSerializerOptions());

                if (command != default)
                {
                    if (command.Id != null && _commands.TryCompleteCommand(command.Id.Value, command))
                        return;

                    var handler = _handlerFactory.GetHandler(command.Protocol);
                    if (handler != null)
                        await handler.HandleAsync(command, _client);
                }
            }
            catch (JsonException ex)
            {
                // TODO: Extract GUID then respond with it
                _logger.LogError(ex, "Error parsing JSON string: {payload}", payload);
            }
        });
        _client.Start();
        exitEvent.WaitOne();
    }
}