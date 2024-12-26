using Kokuban;
using ProjectVTK.Server.CLI.Attributes;

namespace ProjectVTK.Server.CLI.Commands.ConsoleInput;

[ConsoleCommandGroup]
public class GenericConsoleCommands(ConsoleCommandHandler consoleCommands)
{
    private readonly ConsoleCommandHandler _consoleCommand = consoleCommands;
    [ConsoleCommand("help", "Show available commands", "h")]
    public Task PrintHelpAsync()
    {
        var commands = _consoleCommand.GetCommands();

        Console.WriteLine(Chalk.Bold.BrightYellow["Usage:"]);
        Console.WriteLine(Chalk.Green["  command [arguments]"] + Environment.NewLine);
        Console.WriteLine(Chalk.BrightYellow["Available commands:"]);

        var currentHeader = string.Empty;

        // For now, remove aliases with Distinct()
        foreach (var (header, groupDesription, command, description) in commands
            .OrderByDescending(x => string.IsNullOrWhiteSpace(x.HeaderName))
            .DistinctBy(x => x.Description))
        {
            var subcommandPadding = string.IsNullOrWhiteSpace(header) ? "" : "".PadRight(2);

            if (currentHeader != header)
            {
                Console.Write(Environment.NewLine);
                currentHeader = header;

                if (!string.IsNullOrWhiteSpace(header))
                    Console.WriteLine(
                        $"{Chalk.Bold.BrightBlue[header]}" +
                        "".PadLeft(22 - header.Length) +
                        $"{Chalk.Faint.BrightBlue[groupDesription]}"
                    );
            }

            Console.WriteLine(
                $"{subcommandPadding}" +
                $"{Chalk.Bold.BrightGreen[command]}" +
                "".PadLeft(22 - (command.Length + subcommandPadding.Length)) +
                $"{Chalk.Green[description]}"
            );
        }
        return Task.CompletedTask;
    }
}
