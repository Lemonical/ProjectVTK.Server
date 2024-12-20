using Microsoft.Extensions.Hosting;
using ProjectVTK.Server.Core.Services;

namespace ProjectVTK.Server.CLI.Services;

public class ServerBackgroundService(ServerSessions clientService, ServerService serverService) : BackgroundService
{
    private readonly ServerSessions _clientService = clientService;
    private readonly ServerService _serverService = serverService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serverTask = Task.Run(() => _serverService.Start(stoppingToken), stoppingToken);
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
        await serverTask;
    }

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
        var users = _clientService.GetSessions();
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
        var client = _clientService.GetSession(x => x.Id == userId);
        if (client != null)
            client.Socket.Close();
        else
            WriteToConsole("Could not find user by that GUID", ConsoleColor.Red);
    }
}