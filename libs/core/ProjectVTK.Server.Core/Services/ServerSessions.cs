using Fleck;
using ProjectVTK.Server.Core.Models;
using System.Collections.Immutable;

namespace ProjectVTK.Server.Core.Services;

public class ServerSessions(ConfigService configService)
{
    private readonly ConfigService _configService = configService;
    private readonly List<WebSocketSession> _sessions = [];

    /// <summary>
    /// Try to add a session to the server.
    /// </summary>
    /// <param name="session">The WebSocketSession.</param>
    /// <returns>true if session was added successfully, otherwise false.</returns>
    public bool TryAddSession(WebSocketSession session)
    {
        if (_configService.Server == null)
            return false;

        if (_configService.Server.Network.MaxUsers > _sessions.Count)
        {
            _sessions.Add(session);
            return true;
        }

        return false;
    }

    public WebSocketSession? RemoveSession(IWebSocketConnection socket)
    {
        var session = GetSession(socket);
        if (session == null) return null;

        _sessions.Remove(session);
        return session;
    }

    public void ModifySession(Func<WebSocketSession, bool> predicate, Action<WebSocketSession> updateAction)
    {
        var session = _sessions.FirstOrDefault(predicate);
        if (session != null)
            updateAction(session);
    }

    public void Clear()
    {
        _sessions.ForEach(x => x.Close());
        _sessions.Clear();
    }

    public WebSocketSession? GetSession(IWebSocketConnection socket)
        => _sessions.FirstOrDefault(x => x.Socket == socket);

    public WebSocketSession? GetSession(Func<WebSocketSession, bool> predicate)
        => _sessions.FirstOrDefault(predicate);

    public ImmutableHashSet<WebSocketSession> GetSessions()
        => [.. _sessions];

    public ImmutableHashSet<WebSocketSession> GetSessions(Func<WebSocketSession, bool> predicate)
        => _sessions.Where(predicate).ToImmutableHashSet();
}
