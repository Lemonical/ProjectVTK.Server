using Kokuban;
using ProjectVTK.Server.CLI.Attributes;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;

namespace ProjectVTK.Server.CLI.Commands.ConsoleInput;

[ConsoleCommandGroup(HeaderName = "Account", Description = "Account management")]
public class AccountConsoleCommands(ServerService serverService)
{
    private readonly ServerService _serverService = serverService;

    [ConsoleCommand("create", "Create a new account", "new")]
    public async Task CreateAccountAsync(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine(Chalk.BrightBlue[$"Usage: create [username] [password]"]);
            return;
        }

        var createResponse = await _serverService.CreateAccountAsync(args[1], args[2]);
        if (createResponse is not Command createResponseCmd)
        {
            Console.WriteLine(Chalk.BrightRed["Failed to create account: Response received was not in the correct format"]);
            return;
        }

        if (createResponseCmd.Status.GetValueOrDefault() == CommandStatusCode.Failed)
            Console.WriteLine(Chalk.BrightRed[$"Failed to create account: {createResponseCmd.ErrorMessage}"]);
        else
            Console.WriteLine(Chalk.BrightGreen["Account was created successfully"]);
    }

    [ConsoleCommand("login", "Login to an account")]
    public async Task LoginAccountAsync(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine(Chalk.BrightBlue[$"Usage: login [username] [password]"]);
            return;
        }

        var loginResponse = await _serverService.LoginAccountAsync(args[1], args[2]);
        if (loginResponse is not Command loginResponseCmd || loginResponseCmd.Data is not LoginCommandData responseData)
        {
            Console.WriteLine(Chalk.BrightRed["Failed to login: Response received was not in the correct format"]);
            return;
        }

        if (loginResponseCmd.Status.GetValueOrDefault() == CommandStatusCode.Failed)
            Console.WriteLine(Chalk.BrightRed[$"Failed to login: {loginResponseCmd.ErrorMessage}"]);
        else
            Console.WriteLine(Chalk.BrightGreen[$"Logged in as {responseData.Username}"]);
    }
}
