using Fleck;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;
using ProjectVTK.Shared.Helpers;
using System.Net;
using System.Text.Json;
using Websocket.Client;

namespace ProjectVTK.Server.Core.Services;

public class ServerService(CommandHandlerFactory handlerFactory, CommandService commands,
    ServerSessions sessions, ConfigService configService, ILogger<ServerService> logger)
{
    public bool IsCompatibleVersion { get; private set; }
    public bool IsConnected { get; private set; }
    public bool IsLoggedIn { get; private set; }
    public bool IsHosting { get; private set; }
    public const float Version = 1f;

    private readonly CommandHandlerFactory _handlerFactory = handlerFactory;
    private readonly ServerSessions _sessions = sessions;
    private readonly CommandService _commands = commands;
    private readonly ConfigService _configService = configService;
    private readonly ILogger<ServerService> _logger = logger;

    private WebSocketServer? _server;
    private WebsocketClient? _client;
    private NetworkCredential? _credentials;

    // TODO: Change to actual IP
    private const string _masterUrl = "ws://127.0.0.1:6565";
    private bool _hasWarnedDisconnection;

    // Events
    public event EventHandler<WebSocketSession>? SessionOpened;
    public event EventHandler<WebSocketSession>? SessionClosed;
    public event EventHandler<WebSocketSession>? SessionAuthenticated;
    public delegate void SessionMessagedEventHandler(object sender, WebSocketSession session, Command command);
    public event SessionMessagedEventHandler? SessionMessaged;

    public void Start(int port, CancellationToken cancellationToken)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        _server = new WebSocketServer($"ws://0.0.0.0:{port}");

        _server.Start(socket =>
        {
            socket.OnOpen = async () =>
            {
                var ip = socket.ConnectionInfo.ClientIpAddress;
                if (ip == "127.0.0.1" || ip.StartsWith("192.168"))
                {
                    using var httpClient = new HttpClient();
                    ip = (await httpClient.GetStringAsync("http://icanhazip.com"))
                        .Replace("\\r\\n", "").Replace("\\n", "").Trim();
                }

                var session = new WebSocketSession(socket, ip);
                if (!_sessions.TryAddSession(session))
                {
                    _logger.LogWarning("[{ip}] failed to add session. Disconnecting...", session.IpAddress);
                    socket.Close();
                    return;
                }
                SessionOpened?.Invoke(this, session);
                _logger.LogInformation("[{ip}] connected", session.IpAddress);

                var response = await _commands.SendCommandAsync(_client!, Command.CreateRequest(new AuthUserCommandData()
                { IpAddress = session.IpAddress }));

                if (response.Status == CommandStatusCode.Success)
                {
                    var data = (AuthUserCommandData)response.Data!;
                    _sessions.ModifySession(
                        x => x.Socket == socket,
                        x =>
                        {
                            x.Username = data.Username!;
                            x.Id = data.SessionId;
                        }
                    );
                    session.Username = data.Username!;
                    _logger.LogInformation("[{ip}] {username,16} authenticated", session.IpAddress, data.Username);
                    SessionAuthenticated?.Invoke(this, session);
                    await socket.Send(Command.CreateRequest(new ServerStatisticsCommandData()
                    {
                        Description = _configService.Server!.Metadata.Description,
                        Count = new(
                            (ushort)_sessions.GetSessions().Count,
                            _configService.Server.Network.MaxUsers,
                            100, 100, 100 // TODO: Replace with actual count
                        )
                    }).AsJson());
                }
                else
            {
                    _logger.LogWarning("[{ip}] failed to authenticate. Disconnecting...", session.IpAddress);
                    socket.Close();
                }
            };

            socket.OnClose = () =>
            {
                var session = _sessions.RemoveSession(socket);
                _logger.LogInformation("[{ip}] {username,16} disconnected", session?.IpAddress, session?.Username);
                SessionClosed?.Invoke(this, session!);
            };

            socket.OnMessage = async payload =>
            {
                var session = _sessions.GetSession(socket);
                if (string.IsNullOrWhiteSpace(payload) || session == null) return;
                _logger.LogDebug("[{ip}] Received:\n{msg}", session.IpAddress, payload);
                try
                {
                    var command = JsonSerializer.Deserialize<Command>(payload, JsonHelper.GetSerializerOptions());

                    if (command != default)
                    {
                        SessionMessaged?.Invoke(this, session, command);
                        if (command.Id != null && _commands.TryCompleteCommand(command.Id.Value, command))
                            return;

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

        IsHosting = true;
        cancellationToken.WaitHandle.WaitOne();
        Stop();
    }

    public void Stop()
    {
        IsHosting = false;
        _server?.Dispose();
        _client?.Dispose();
        _logger.LogInformation("Server stopped");
    }

    public void ConnectToMaster()
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

            IsLoggedIn = IsConnected = IsCompatibleVersion = false;
            _hasWarnedDisconnection = true;
            _logger.LogWarning("Disconnected from master server");
        });
        _client.ReconnectionHappened.Subscribe(async _ =>
        {
            IsConnected = true;
            _hasWarnedDisconnection = false;
            _logger.LogInformation("Connected to master server");
            var vcResponse = await _commands.SendCommandAsync(_client,
                Command.CreateRequest(
                    new VersionCheckCommandData()
                    { Type = ClientType.Server, Version = Version }
                ));

            IsCompatibleVersion = vcResponse.Status == CommandStatusCode.Success;
            if (!IsCompatibleVersion)
                _logger.LogError("Running version {version}, but it is not compatible!", Version);
            else
            {
                _logger.LogInformation("{message}", ((VersionCheckCommandData)vcResponse.Data!).Message);
                if (_credentials != null)
                    await LoginAccountAsync(_credentials.UserName, _credentials.Password);
            }
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

    public async Task<Command?> LoginAccountAsync(string username, string hashedPassword)
    {
        if (!IsConnected || !IsCompatibleVersion) return null;

        var response = await _commands.SendCommandAsync(_client!,
            Command.CreateRequest(
                new LoginCommandData()
                { Username = username, Password = hashedPassword }
            ));
        if (response.Status == CommandStatusCode.Success)
        {
            IsLoggedIn = true;
            if (_credentials == null || _credentials.UserName != username && _credentials.Password != hashedPassword)
            {
                //var secureString = new SecureString();
                //foreach (var pass in hashedPassword)
                //    secureString.AppendChar(pass);
                _credentials = new(username, hashedPassword);
            }
        }

        return response;
    }

    public async Task<Command?> CreateAccountAsync(string username, string password)
    {
        if (!IsConnected || !IsCompatibleVersion) return null;

        var response = await _commands.SendCommandAsync(_client!,
            Command.CreateRequest(
                new CreateAccountCommandData()
                { Username = username, Password = StringHelper.Hash(password) }
            ));

        return response;
    }

    public async Task<Command?> PublicizeServerAsync(string? name = null, int? port = null, ushort? userCount = null, ushort? maxUserCount = null)
    {
        if (!IsConnected || !IsCompatibleVersion || !IsLoggedIn) return null;

        var response = await _commands.SendCommandAsync(_client!,
            Command.CreateRequest(
                new PublicizeServerCommandData()
                { 
                    Name = name ?? _configService.Server!.Metadata.Name,
                    Port = port ?? _configService.Server!.Network.Port,
                    UserCount = userCount ?? (ushort)_sessions.GetSessions().Count,
                    MaxUserCount = maxUserCount ?? _configService.Server!.Network.MaxUsers
                }
            ));

        return response;
    }

    public async Task<Command?> UpdateServerAsync(string name, ushort userCount, ushort maxUserCount, bool isPublic)
    {
        if (!IsConnected || !IsCompatibleVersion || !IsLoggedIn) return null;

        var response = await _commands.SendCommandAsync(_client!,
            Command.CreateRequest(
                new UpdateServerCommandData()
                {
                    Name = name,
                    ListServer = isPublic,
                    UserCount = userCount,
                    MaxUserCount = maxUserCount
                }
            ));

        return response;
    }
}