using Fleck;
using ProjectVTK.Server.Core.Models;
using ProjectVTK.Server.Core.Services;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Commands.Data;
using ProjectVTK.Shared.Helpers;

namespace ProjectVTK.Server.Core.Commands;

public class GetServerMetadataCommandHandler(ServerSessions sessions, ConfigService configService) : ICommandHandler
{
    private readonly ServerSessions _sessions = sessions;
    private readonly ConfigService _configService = configService;

    public bool CanHandle(CommandProtocols protocol)
        => protocol == CommandProtocols.GetServerMetadata;

    public async Task HandleAsync(Command command, object socketObj)
    {
        if (command.Id is not Guid guid || guid == default || socketObj is not IWebSocketConnection socket ||
            _sessions.GetSession(socket) is not WebSocketSession session || !session.HasResolved)
            return;

        var metadata = new GetServerMetadataCommandData()
        {
            Areas = _configService.Areas,
            Characters = _configService.Characters,
            Music = _configService.Music,
            Offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)
        };

        await socket.Send(Command.CreateResponse(guid, metadata, CommandStatusCode.Success).AsJson());
    }
}
