using Fleck;
using ProjectVTK.Server.Core.Models;
using System.Collections.Immutable;

namespace ProjectVTK.Server.Core.Services;

public class ClientService
{
    private readonly List<Client> _clients = [];

    public void AddClient(Client client)
        => _clients.Add(client);

    public Client? RemoveClient(IWebSocketConnection socket)
    {
        var client = GetClient(socket);
        if (client == null) return null;

        _clients.Remove(client);
        return client;
    }

    public void UpdateClient(Client oldClient, Client newClient)
    {
        var index = _clients.IndexOf(oldClient);
        _clients[index] = newClient;
    }

    public void Clear()
        => _clients.Clear();

    public Client? GetClient(IWebSocketConnection socket)
        => _clients.FirstOrDefault(x => x.Socket == socket);

    public Client? GetClient(Func<Client, bool> predicate)
        => _clients.FirstOrDefault(predicate);

    public ImmutableHashSet<Client> GetClients()
        => [.. _clients];

    public ImmutableHashSet<Client> GetClients(Func<Client, bool> predicate)
        => _clients.Where(predicate).ToImmutableHashSet();
}
