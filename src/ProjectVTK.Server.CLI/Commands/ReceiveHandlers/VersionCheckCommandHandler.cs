using Microsoft.Extensions.Logging;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;

namespace ProjectVTK.Server.CLI.Commands.ReceiveHandlers;

public class VersionCheckCommandHandler(ILogger<VersionCheckCommandHandler> logger, CommandService commands) : ICommandHandler
{
    private readonly ILogger<VersionCheckCommandHandler> _logger = logger;
    private readonly CommandService _commands = commands;

    public bool CanHandle(CommandProtocols protocol)
        => protocol == CommandProtocols.VersionCheck;

    public Task HandleAsync(Command command, object socketObject)
    {
        if (command.Data is not VersionCheckCommandData vcData || command.Id is not Guid guid || guid == default)
            return Task.CompletedTask;

        _commands.TryCompleteCommand(guid, command);
        return Task.CompletedTask;
    }
}
