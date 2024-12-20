using Fleck;
using ProjectVTK.Server.Core.Models;
using System.Collections.Immutable;

namespace ProjectVTK.Server.Core.Services;

public class ServerSessions
{
    private readonly List<WebSocketSession> _sessions = [];

    public void AddSession(WebSocketSession session)
        => _sessions.Add(session);

    public WebSocketSession? RemoveSession(IWebSocketConnection socket)
    {
        var session = GetSession(socket);
        if (session == null) return null;

        _sessions.Remove(session);
        return session;
    }

    public void UpdateSession(WebSocketSession oldSession, WebSocketSession newSession)
    {
        var index = _sessions.IndexOf(oldSession);
        _sessions[index] = newSession;
    }

    public void Clear()
        => _sessions.Clear();

    public WebSocketSession? GetSession(IWebSocketConnection socket)
        => _sessions.FirstOrDefault(x => x.Socket == socket);

    public WebSocketSession? GetSession(Func<WebSocketSession, bool> predicate)
        => _sessions.FirstOrDefault(predicate);

    public ImmutableHashSet<WebSocketSession> GetSessions()
        => [.. _sessions];

    public ImmutableHashSet<WebSocketSession> GetSessions(Func<WebSocketSession, bool> predicate)
        => _sessions.Where(predicate).ToImmutableHashSet();
}
