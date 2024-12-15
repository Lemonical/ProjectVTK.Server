using Fleck;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Helpers;
using System.Text.Json;
using Websocket.Client;

namespace ProjectVTK.Server.Core.Services;

public class ServerService
{
    private readonly ConfigService _configService;
    private readonly CommandHandlerFactory _handlerFactory;
    private readonly ClientService _clientService;
    private readonly ILogger<ServerService> _logger;
    private WebSocketServer? _server;
    private WebsocketClient? _client;

    // TODO: Change to actual IP
    private const string _masterUrl = "ws://127.0.0.1:6565";
    private bool _hasWarnedDisconnection;

    public ServerService(CommandHandlerFactory handlerFactory, ConfigService configService, ClientService clientService, ILogger<ServerService> logger)
    {
        _handlerFactory = handlerFactory;
        _configService = configService;
        _clientService = clientService;
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
                var client = new Client(socket);
                _clientService.AddClient(client);
                _logger.LogInformation("[{ip,-16}] connected", socket.ConnectionInfo.ClientIpAddress);
            };

            socket.OnClose = () =>
            {
                var client = _clientService.RemoveClient(socket);
                _logger.LogInformation("[{ip,-16}] {username,16} disconnected", socket.ConnectionInfo.ClientIpAddress, client?.Username);
            };

            socket.OnMessage = async payload =>
            {
                try
                {
                    var command = JsonSerializer.Deserialize<Command>(payload, JsonHelper.GetSerializerOptions());

                    if (command != default)
                    {
                        var handler = _handlerFactory.GetHandler(command.Protocol);
                        if (handler != null)
                            await handler.HandleAsync(command, socket);
                        else
                            await socket.Send(
                                Command.CreateResponse(
                                    command.Id.GetValueOrDefault(), command.Protocol, CommandStatusCode.Failed, "Unknown protocol"));
                    }
                    else
                        await socket.Send(
                            Command.CreateResponse(
                                command.Id.GetValueOrDefault(), command.Protocol, CommandStatusCode.Failed, "Unknown protocol"));
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
        _client.ReconnectionHappened.Subscribe(_ =>
        {
            _hasWarnedDisconnection = false;
            _logger.LogInformation("Connected to master server");
        });
        _client.MessageReceived.Subscribe(msg =>
        {
            _logger.LogDebug("[Master] Received:\n{msg}", msg);
        });
        _client.Start();
        exitEvent.WaitOne();
    }
}