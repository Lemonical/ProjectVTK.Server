using Fleck;
using Microsoft.Extensions.Hosting;
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

    private const string _masterUrl = "ws://127.0.0.1:6565";

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
        _logger.LogInformation("Connecting to {url}", _masterUrl);
        _client = new WebsocketClient(new(_masterUrl))
        {
            ReconnectTimeout = null,
            ErrorReconnectTimeout = TimeSpan.FromSeconds(10),
            LostReconnectTimeout = TimeSpan.FromSeconds(10)
        };

        _client.ReconnectionHappened.Subscribe(_ => _logger.LogInformation("Connected to master server"));
        _client.MessageReceived.Subscribe(msg =>
        {
            _logger.LogDebug("[Master] Received:\n{msg}", msg);
        });
        _client.Start();
        exitEvent.WaitOne();
    }
}

public class ServerBackgroundService(ClientService clientService, ServerService serverService) : BackgroundService
{
    private readonly ClientService _clientService = clientService;
    private readonly ServerService _serverService = serverService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serverTask = Task.Run(() => _serverService.Start(stoppingToken), stoppingToken);
#if CONSOLE_APP
        WriteToConsole("Type 'help' for available commands.", ConsoleColor.Green);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                string? input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input)) continue;

                await ProcessCommandAsync(input);
            }
            await Task.Delay(25, stoppingToken);
        }
#endif
        await serverTask;
    }

#if CONSOLE_APP
    private Task ProcessCommandAsync(string input)
    {
        var args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return Task.CompletedTask;

        var command = args[0].ToLower();
        switch (command)
        {
            case "help":
                PrintHelp();
                break;

            case "users":
                ShowConnectedClients();
                break;

            case "kick":
                if (args.Length < 2)
                {
                    WriteToConsole($"Usage: kick <guid>", ConsoleColor.DarkBlue);
                    return Task.CompletedTask;
                }
                if (Guid.TryParse(args[1], out var userId))
                    DisconnectClient(userId);
                else
                    WriteToConsole($"Invalid GUID", ConsoleColor.Red);
                break;

            default:
                WriteToConsole($"Unknown command: {command}", ConsoleColor.Red);
                break;
        }

        return Task.CompletedTask;
    }

    private static void WriteToConsole(string text, ConsoleColor? color = null)
    {
        if (color != null)
            Console.ForegroundColor = color.Value;
        Console.WriteLine(text);
        if (color != null)
            Console.ResetColor();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help           - Show available commands");
        Console.WriteLine("  users          - Show list of online users");
        Console.WriteLine("  kick <guid>    - Disconnect a user by their session GUID");
    }

    private void ShowConnectedClients()
    {
        var users = _clientService.GetClients();
        if (users.Count == 0)
        {
            Console.WriteLine("No users are currently online.");
            return;
        }

        Console.WriteLine("Online users:");
        foreach (var user in users)
            Console.WriteLine($"- {user.Id}: {user.Username}");
    }

    private void DisconnectClient(Guid userId)
    {
        var client = _clientService.GetClient(x => x.Id == userId);
        if (client != null)
            client.Socket.Close();
        else
            WriteToConsole("Could not find user by that GUID", ConsoleColor.Red);
    }
#endif
}