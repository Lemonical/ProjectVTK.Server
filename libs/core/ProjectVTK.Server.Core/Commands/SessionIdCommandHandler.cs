using Fleck;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;

namespace ProjectVTK.Server.Core.Commands;

public class SessionIdCommandHandler(ServerSessions sessions) : ICommandHandler
{
    private readonly ServerSessions _sessions = sessions;

    public bool CanHandle(CommandProtocols protocol)
        => protocol == CommandProtocols.SessionId;

    public Task HandleAsync(Command command, object socketObj)
    {
        if (command.Data is not SessionIdCommandData sessionData || command.Id is not Guid guid || guid == default ||
            socketObj is not IWebSocketConnection socket || _sessions.GetSession(socket) is not WebSocketSession session || 
            session.HasResolved || sessionData.SessionId == default)
            return Task.CompletedTask;

        _sessions.ModifySession(
            x => x.Socket == socket,
            x => x.Id = sessionData.SessionId
        );

        return Task.CompletedTask;
    }
}
