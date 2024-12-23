using Fleck;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Helpers;
using ProjectVTK.Shared.Models.Interfaces;

namespace ProjectVTK.Server.Core.Models;

public record WebSocketSession(IWebSocketConnection Socket)
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public IArea? Area { get; set; } = null;

    public ICharacter? Character { get; set; } = null;

    public string IpAddress => Socket.ConnectionInfo.ClientIpAddress;

    public void Close()
        => Socket.Close();

    public async Task Send(Command command)
        => await Socket.Send(command.AsJson());
}