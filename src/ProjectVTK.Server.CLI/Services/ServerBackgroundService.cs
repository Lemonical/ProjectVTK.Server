using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;

namespace ProjectVTK.Server.CLI.Services;

// TODO: Implement a better way to handle console commands
public class ServerBackgroundService(ServerSessions sessions, ServerService serverService, ConfigService configService, ILogger<ServerBackgroundService> logger) : BackgroundService
{
    private readonly ServerSessions _sessions = sessions;
    private readonly ServerService _serverService = serverService;
    private readonly ConfigService _configService = configService;
    private readonly ILogger<ServerBackgroundService> _logger = logger;
    private bool _serverStarted = false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.Title = "Server CLI";

        // This shouldn't fail, but just in case
        if (_configService.Server == null)
        {
            _logger.LogCritical("Failed to load server configurations");
            return;
        }

        var masterServerTask = Task.Run(_serverService.ConnectToMaster, stoppingToken);
        Task? serverTask = null;
        PrintToConsole("Type 'help' for available commands.", ConsoleColor.Green);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                Console.Write("> ");
                string? input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input)) continue;

                await ProcessCommandAsync(input);

                if (_serverStarted && serverTask == null)
                {
                    Console.Title = _configService.Server.Metadata.Name;
                    serverTask = Task.Run(() => _serverService.Start(_configService.Server.Network.Port, stoppingToken), stoppingToken);
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
        var args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return;

        var command = args[0].ToLower();
        switch (command)
        {
            case "help":
                PrintHelp();
                break;

            case "users":
                ShowSessions();
                break;

            // TODO: Implement multi user disconnect
            case "kick":
                if (args.Length != 2)
                {
                    PrintToConsole($"Usage: kick [users...]", ConsoleColor.DarkBlue);
                    break;
                }
                if (Guid.TryParse(args[1], out var userId) && _sessions.GetSession(x => x.Id == userId) is { } userByGuid)
                {
                    userByGuid.Socket.Close();
                    PrintToConsole($"Disconnected user by GUID {userId}", ConsoleColor.Green);
                }
                else if (_sessions.GetSession(
                    x => x.Username.Equals(args[1], StringComparison.InvariantCultureIgnoreCase)) is { } user)
                {
                    user.Socket.Close();
                    PrintToConsole($"Disconnected user {args[1]}", ConsoleColor.Green);
                }
                else if (_sessions.GetSession(x => x.IpAddress == args[1]) is { } userByIp)
                {
                    userByIp.Socket.Close();
                    PrintToConsole($"Disconnected user by IP {args[1]}", ConsoleColor.Green);
                }
                else
                    PrintToConsole($"Invalid args", ConsoleColor.Red);
                break;

            case "ban":
                if (args.Length < 2)
                {
                    PrintToConsole($"Usage: ban [users...]", ConsoleColor.DarkBlue);
                    break;
                }
                string[] bans = [.. BanUsernames(args.Skip(1).ToArray()), .. BanIpAddresses(args.Skip(1).ToArray())];
                PrintToConsole($"Banned user(s): {string.Join(',', bans)}", ConsoleColor.Green);

                if (bans.Length == 0)
                    PrintToConsole("No users were banned", ConsoleColor.Red);
                break;

            case "create":
                if (args.Length != 3)
                {
                    PrintToConsole($"Usage: create [username] [password]", ConsoleColor.DarkBlue);
                    break;
                }

                var createResponse = await _serverService.CreateAccountAsync(args[1], args[2]);
                if (createResponse is not Command createResponseCmd) break;
                if (createResponseCmd.Status.GetValueOrDefault() == CommandStatusCode.Failed)
                    PrintToConsole($"Failed to create account: {createResponseCmd.ErrorMessage}", ConsoleColor.Red);
                else
                    PrintToConsole("Account created successfully", ConsoleColor.Green);
                break;

            case "login":
                if (args.Length != 3)
                {
                    PrintToConsole($"Usage: login [username] [password]", ConsoleColor.DarkBlue);
                    break;
                }
                var loginResponse = await _serverService.LoginAccountAsync(args[1], args[2]);
                if (loginResponse is not Command loginResponseCmd) break;
                if (loginResponseCmd.Status.GetValueOrDefault() == CommandStatusCode.Failed)
                    PrintToConsole($"Failed to login: {loginResponseCmd.ErrorMessage}", ConsoleColor.Red);
                else
                    PrintToConsole($"Logged in as {args[1]}", ConsoleColor.Green);
                break;

            case "host":
                if (!_serverService.IsCompatibleVersion)
                {
                    PrintToConsole("Server is not using a compatible version", ConsoleColor.Red);
                    break;
                }
                else if (!_serverService.IsConnected)
                {
                    PrintToConsole("Server is not connected to the master server", ConsoleColor.Red);
                    break;
                }
                else if (!_serverService.IsLoggedIn)
                {
                    PrintToConsole("You are not logged in", ConsoleColor.Red);
                    break;
                }
                _serverStarted = true;
                break;

            default:
                PrintToConsole($"Unknown command: {command}", ConsoleColor.Red);
                break;
        }
    }

    private static void PrintToConsole(string text, ConsoleColor? color = null, bool newLine = true)
    {
        if (color != null)
            Console.ForegroundColor = color.Value;
        if (newLine)
            Console.WriteLine(text);
        else
            Console.Write(text);
        if (color != null)
            Console.ResetColor();
    }

    private static void PrintHelp()
    {
        // Header:      ConsoleColor.Yellow
        // Description: ConsoleColor.DarkGreen
        // Name:        ConsoleColor.Green
        // Practice indentation for easier reading

        PrintToConsole("Usage:", ConsoleColor.Yellow);
        PrintToConsole("  command [arguments]" + Environment.NewLine, ConsoleColor.DarkGreen);

        string[] commands = 
        [
            "users",
            "kick [users...]",
            "ban [users...]",
            "login [username] [password]",
            "create [username] [password]",
            "host"
        ];

        string[] commandHelp =
        [
            "Show list of online users",
            "Disconnect user(s) by their name, IP address, or session GUID",
            "Disconnect and ban user(s) by their name or IP address",
            "Log into account",
            "Create an account",
            "Start hosting server"
        ];

        var padding = commands.Max(x => x.Length) + 6; // 6 is const padding
        PrintToConsole("Available commands:", ConsoleColor.Yellow);

        for (int i = 0; i < commands.Length; i++)
        {
            PrintToConsole(string.Format("  {0}", commands[i].PadRight(padding)), ConsoleColor.Green, false);
            PrintToConsole($"{commandHelp[i]}", ConsoleColor.DarkGreen, true);
        }
    }

    private void ShowSessions()
    {
        var users = _sessions.GetSessions();

        if (users.Count == 0)
        {
            Console.WriteLine("No users are currently online.");
            return;
        }

        string usernameHead = "Username",
            areaHead = "Area",
            characterHead = "Character",
            guidHead = "Session GUID",
            ipHead = "IP Address";

        int usernameColPad = 16,
            areaColPad = Math.Max(areaHead.Length, users
                .Where(x => x.Area != null).Max(x => x.Area!.Name.Length)),
            characterColPad = Math.Max(characterHead.Length, users
                .Where(x => x.Character != null).Max(x => x.Character!.FolderName.Length)),
            guidColPad = 36,
            ipAddressColPad = 15;

        var headers = string.Format("| {0} | {1} | {2} | {3} | {4} |",
            usernameHead.PadLeft((usernameColPad + usernameHead.Length) / 2)
                .PadRight(usernameColPad),
            areaHead.PadLeft((areaColPad + areaHead.Length) / 2)
                .PadRight(areaColPad),
            characterHead.PadLeft((characterColPad + characterHead.Length) / 2)
                .PadRight(characterColPad),
            ipHead.PadLeft((ipAddressColPad + ipHead.Length) / 2)
                .PadRight(ipAddressColPad),
            guidHead.PadLeft((guidColPad + guidHead.Length) / 2)
                .PadRight(guidColPad));

        var onlineUsersText = $"{users.Count} ONLINE USERS" + Environment.NewLine;
        var onlineUsersFormatted = onlineUsersText.PadLeft((headers.Length + onlineUsersText.Length) / 2).PadRight(headers.Length / 2);

        var rowSeparator = string.Format("|{0}|{1}|{2}|{3}|{4}|",
            "-".PadRight(usernameColPad + 2, '-'),
            "-".PadRight(areaColPad + 2, '-'),
            "-".PadRight(characterColPad + 2, '-'),
            "-".PadRight(ipAddressColPad + 2, '-'),
            "-".PadRight(guidColPad + 2, '-'));

        PrintToConsole(onlineUsersFormatted, ConsoleColor.Green);
        PrintToConsole(headers, ConsoleColor.Green);
        PrintToConsole(rowSeparator, ConsoleColor.Green);
        foreach (var user in users)
        {
            var format = string.Format("| {0} | {1} | {2} | {3} | {4} |",
                user.Username.PadRight(usernameColPad),
                (user.Area?.Name ?? "").PadRight(areaColPad),
                (user.Character?.FolderName ?? "").PadRight(characterColPad),
                user.IpAddress.PadRight(ipAddressColPad),
                user.Id.ToString().PadRight(guidColPad));

            PrintToConsole(format, ConsoleColor.Green);
        }
        PrintToConsole(rowSeparator.Replace('|', '-'), ConsoleColor.Green);
    }

    private string[] BanUsernames(params string[] usernames)
    {
        foreach (var username in usernames)
        {
            var client = _sessions.GetSession(x => x.Username.Equals(username, StringComparison.InvariantCultureIgnoreCase));
            if (client == null)
            {
                PrintToConsole($"User {username} was not found", ConsoleColor.Red);
                // Remove the username from array
                usernames = usernames.Where(x => !x.Equals(username, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                continue;
            }
            client.Socket.Close();
        }
        if (usernames.Length == 0) return [];
        _configService.AddBannedUsernames(usernames);
        return usernames;
    }

    private string[] BanIpAddresses(params string[] ipAddresses)
    {
        foreach (var ip in ipAddresses)
        {
            var client = _sessions.GetSession(x => x.IpAddress == ip);
            if (client == null)
            {
                PrintToConsole($"User with IP {ip} was not found", ConsoleColor.Red);
                // Remove the IP address from array
                ipAddresses = ipAddresses.Where(x => x != ip).ToArray();
                continue;
            }
            client.Socket.Close();
        }
        if (ipAddresses.Length == 0) return [];
        _configService.AddBannedIpAddresses(ipAddresses);
        return ipAddresses;
    }
}
