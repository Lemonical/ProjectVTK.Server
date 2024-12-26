using Kokuban;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.CLI.Commands.ConsoleInput;
using ProjectVTK.Server.Core.Services;

namespace ProjectVTK.Server.CLI.Services;

public class ServerBackgroundService(ServerService serverService, ConfigService configService,
    ConsoleCommandHandler consoleCommandHandler, ServerState serverState, ILogger<ServerBackgroundService> logger) : BackgroundService
{
    private readonly ServerService _serverService = serverService;
    private readonly ConfigService _configService = configService;
    private readonly ConsoleCommandHandler _consoleCommandHandler = consoleCommandHandler;
    private readonly ServerState _serverState = serverState;
    private readonly ILogger<ServerBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This shouldn't fail, but just in case
        if (_configService.Server == null)
        {
            _logger.LogCritical("Failed to load server configurations");
            return;
        }

        var masterServerTask = Task.Run(_serverService.ConnectToMaster, stoppingToken);
        Task? serverTask = null;
        Console.WriteLine(Chalk.BrightGreen["Type 'help' for available commands."]);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                Console.Write("> ");
                string? input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input)) continue;

                await ProcessCommandAsync(input);

                if (_serverState.HasStarted && serverTask == null && _serverService.IsLoggedIn)
                {
                    using var httpClient = new HttpClient();
                    var publicIp = (await httpClient.GetStringAsync("http://icanhazip.com", stoppingToken))
                        .Replace("\\r\\n", "").Replace("\\n", "").Trim();

                    Console.Title = string.Concat(
                        $"[{(_configService.Server.Network.IsPublic ? "Public" : "Private")}] ",
                        _configService.Server.Metadata.Name,
                        "@", _configService.Server.Network.Port);
                    serverTask = Task.Run(() => _serverService.Start(_configService.Server.Network.Port, stoppingToken), stoppingToken);
                    _logger.LogInformation("Hosting {name} @ ws://{publicIp}:{port} with {maxUsers} max users",
                        _configService.Server.Metadata.Name, publicIp, _configService.Server.Network.Port, _configService.Server.Network.MaxUsers);
                }
                else if (!_serverState.HasStarted && serverTask != null)
                {
                    serverTask.Dispose();
                    serverTask = null;
                }
            }
            await Task.Delay(25, stoppingToken);
        }

        if (serverTask != null)
            await Task.WhenAll(masterServerTask, serverTask);
        else
            await masterServerTask;
    }

    private async Task ProcessCommandAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        await _consoleCommandHandler.ExecuteCommandAsync(input);
    }
}
