using Fleck;

namespace ProjectVTK.Server.Core.Models;

public record Client(IWebSocketConnection Socket)
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string IpAddress => Socket.ConnectionInfo.ClientIpAddress;
}