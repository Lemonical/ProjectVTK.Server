using Kokuban;
using ProjectVTK.Server.CLI.Attributes;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Server.Core.Services;

namespace ProjectVTK.Server.CLI.Commands.ConsoleInput;

[ConsoleCommandGroup(HeaderName = "Session", Description = "Session management")]
public class SessionConsoleCommands(ServerSessions sessions, ConfigService configService)
{
    private readonly ServerSessions _sessions = sessions;
    private readonly ConfigService _configService = configService;

    [ConsoleCommand("users", "Show all online users", "list", "sessions")]
    public Task ShowSessionsAsync(string[] args)
    {
        var users = _sessions.GetSessions();

        if (users.Count == 0)
        {
            Console.WriteLine("No users are currently online.");
            return Task.CompletedTask;
        }

        string usernameHead = "Username",
            areaHead = "Area",
            characterHead = "Character",
            guidHead = "Session GUID",
            ipHead = "IP Address";

        int usernameColPad = 16,
            areaColPad = Math.Max(areaHead.Length, users
                .MaxBy(x => x.Area?.Name.Length)?.Area?.Name.Length ?? 0),
            characterColPad = Math.Max(characterHead.Length, users
                .MaxBy(x => x.Character?.FolderName.Length)?.Character?.FolderName.Length ?? 0),
            guidColPad = 36,
            ipAddressColPad = 15;

        var headers = string.Format("│ {0} │ {1} │ {2} │ {3} │ {4} │",
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

        var onlineUsersText = $"{users.Count} ONLINE USERS";
        var onlineUsersFormatted = onlineUsersText.PadLeft((headers.Length + onlineUsersText.Length) / 2).PadRight(headers.Length / 2);

        var rowSeparator = string.Format("├{0}┼{1}┼{2}┼{3}┼{4}┤",
            "─".PadRight(usernameColPad + 2, '─'),
            "─".PadRight(areaColPad + 2, '─'),
            "─".PadRight(characterColPad + 2, '─'),
            "─".PadRight(ipAddressColPad + 2, '─'),
            "─".PadRight(guidColPad + 2, '─'));

        var defaultColor = Chalk.Green;
        Console.WriteLine(defaultColor[onlineUsersFormatted] + Environment.NewLine);
        Console.WriteLine(defaultColor[rowSeparator.Replace('┼', '┬').Replace('├', '┌').Replace('┤', '┐')]);
        Console.WriteLine(defaultColor[headers]);
        Console.WriteLine(defaultColor[rowSeparator]);
        foreach (var user in users)
        {
            var div = defaultColor["│"];
            var format = string.Format($"{div} {{0}} {div} {{1}} {div} {{2}} {div} {{3}} {div} {{4}} {div}",
                user.Username.PadRight(usernameColPad),
                (user.Area?.Name ?? "").PadRight(areaColPad),
                (user.Character?.FolderName ?? "").PadRight(characterColPad),
                user.IpAddress.PadRight(ipAddressColPad),
                user.Id.ToString().PadRight(guidColPad));

            Console.WriteLine(format);
        }
        Console.WriteLine(defaultColor[rowSeparator.Replace('┼', '┴').Replace('├', '└').Replace('┤', '┘')]);

        return Task.CompletedTask;
    }

    [ConsoleCommand("kick", "Kick user(s) from the server", "disconnect", "dc")]
    public Task KickSessionsAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(Chalk.BrightBlue[$"Usage: kick [users...]"]);
            Console.WriteLine($"   Accepts {Chalk.Underline["Session GUID"]}," +
                $" {Chalk.Underline["IP address"]}," +
                $" and {Chalk.Underline["Username"]}");
            return Task.CompletedTask;
        }
        WebSocketSession[] sessions = GetSessions(args);

        if (sessions.Length == 0)
        {
            Console.WriteLine(Chalk.BrightRed["Invalid args"]);
            return Task.CompletedTask;
        }

        foreach (var session in sessions)
            session.Socket.Close();

        return Task.CompletedTask;
    }

    [ConsoleCommand("ban", "Ban user(s) from the server")]
    public Task BanSessionsAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(Chalk.BrightBlue[$"Usage: ban [users...]"]);
            Console.WriteLine($"   Accepts {Chalk.Underline["IP address"]}," +
                $" and {Chalk.Underline["Username"]}");
            return Task.CompletedTask;
        }
        string[] bans = [.. BanUsernames(args), .. BanIpAddresses(args)];
        Console.WriteLine($"{Chalk.BrightGreen["Banned user(s)"]}: {string.Join(',', bans)}");

        if (bans.Length == 0)
            Console.WriteLine(Chalk.BrightRed["Invalid args"]);

        return Task.CompletedTask;
    }

    private WebSocketSession[] GetSessions(params string[] users)
    {
        return users
            .Select(user =>
            {
                if (Guid.TryParse(user, out var userId))
                    return _sessions.GetSession(x => x.Id == userId);

                return _sessions.GetSession(x => x.Username.Equals(user, StringComparison.OrdinalIgnoreCase))
                    ?? _sessions.GetSession(x => x.IpAddress == user);
            })
            .Where(session => session != null)
            .Cast<WebSocketSession>()
            .ToArray();
    }

    private string[] BanUsernames(params string[] usernames)
    {
        foreach (var username in usernames)
        {
            var client = _sessions.GetSession(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (client == null)
            {
                Console.WriteLine(Chalk.BrightRed[$"User {username} was not found"]);
                // Remove the username from array
                usernames = [.. usernames.Where(x => !x.Equals(username, StringComparison.OrdinalIgnoreCase))];
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
                Console.WriteLine(Chalk.BrightRed[$"User with IP {ip} was not found"]);
                // Remove the IP address from array
                ipAddresses = [.. ipAddresses.Where(x => x != ip)];
                continue;
            }
            client.Socket.Close();
        }
        if (ipAddresses.Length == 0) return [];
        _configService.AddBannedIpAddresses(ipAddresses);
        return ipAddresses;
    }
}
