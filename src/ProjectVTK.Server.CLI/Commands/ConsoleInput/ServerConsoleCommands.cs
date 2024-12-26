using Kokuban;
using ProjectVTK.Server.CLI.Attributes;
using ProjectVTK.Server.Core.Services;

namespace ProjectVTK.Server.CLI.Commands.ConsoleInput;

[ConsoleCommandGroup(HeaderName = "Server", Description = "Server management")]
public class ServerConsoleCommands(ServerService serverService, ServerState serverState)
{
    private readonly ServerService _serverService = serverService;
    private readonly ServerState _serverState = serverState;

    [ConsoleCommand("host", "Start the server", "start", "s")]
    public Task HostServerAsync()
    {
        if (!_serverService.IsCompatibleVersion)
            Console.WriteLine(Chalk.BrightRed["Server is not using a compatible version"]);
        else if (!_serverService.IsConnected)
            Console.WriteLine(Chalk.BrightRed["Server is not connected to the master server"]);
        else if (!_serverService.IsLoggedIn)
            Console.WriteLine(Chalk.BrightRed["You are not logged in"]);
        else
            _serverState.StartServer();

        return Task.CompletedTask;
    }
}
