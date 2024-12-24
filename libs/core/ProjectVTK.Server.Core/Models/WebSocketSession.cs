using Fleck;
using ProjectVTK.Shared.Commands;
using ProjectVTK.Shared.Helpers;
using ProjectVTK.Shared.Models.Interfaces;

namespace ProjectVTK.Server.Core.Models;

public record WebSocketSession(IWebSocketConnection Socket, string IpAddress)
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public IArea? Area { get; set; } = null;

    public ICharacter? Character { get; set; } = null;

    public bool HasResolved => !string.IsNullOrWhiteSpace(Username) && Id != default;

    public void Close()
        => Socket.Close();

    public async Task Send(Command command)
        => await Socket.Send(command.AsJson());
}