
using System.Net.WebSockets;
using RoomArchServer;

namespace RoomArch.Models;

public class Room
{
    public readonly string Name;
    public string NormalizedName {get => Name.ToLower().Trim();}
    private readonly List<Client> _clients = new List<Client>();

    public readonly Client Host;
    public string? Password;
    public bool Locked = false;
    public int ClientLimit = 5;
    public int Count {get => _clients.Count;}

    public Room(RoomConfiguration configuration, Client host)
    {
        Name = configuration.Name;
        Password = configuration.Password;
        Host = host;
        _clients.Add(Host);
    }

    public void AddClient(Client client)
    {
        _clients.Add(client);
        Broadcast(client, new Notification(client.Name, true)).Wait();
    }

    public bool RemoveClient(Client client)
    {
        bool removed = _clients.Remove(client);

        if (removed)
        {
            if (client == Host)
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    WebSocketEndpoint.SendSafe(_clients[i].WebSocket, 
                        new Notification(NotificationCode.KickedOut).UTF8Bytes).Wait();
                    _clients[i].Room = null;
                }

                _clients.Clear();
            }
            else Broadcast(client, new Notification(client.Name, false)).Wait();
        }
        
        return removed;
    }

    public bool HasClient(Client client)
    {
        return _clients.Contains(client);
    }

    public bool HasClient(string name)
    {
        name = name.ToLower().Trim();
        Client? client = _clients.Find(client => client.NormalizedName == name);
        return client is not null;
    }

    public async Task Broadcast(Client sender, Notification notification)
    {
        foreach (Client client in _clients)
        {
            if (client != sender)
                await WebSocketEndpoint.SendSafe(client.WebSocket, notification.UTF8Bytes);
        }
    }

    public async Task Broadcast(Client sender, Notification notification, string[] receivers)
    {
        foreach (string receiver in receivers)
        {
            string normalizedName = receiver.ToLower().Trim();

            Client? client = _clients.Find(client => client.NormalizedName == normalizedName);

            if (client is not null)
                await WebSocketEndpoint.SendSafe(client.WebSocket, notification.UTF8Bytes);
        }
    }

    public async Task RemoveClients(Client sender, Notification notification, string[] receivers)
    {
        foreach (string receiver in receivers)
        {
            string normalizedName = receiver.ToLower().Trim();

            Client? client = _clients.Find(client => client.NormalizedName == normalizedName);

            if (client is not null)
            {
                client.Room = null;
                await WebSocketEndpoint.SendSafe(client.WebSocket, notification.UTF8Bytes);
            }
        }
    }

}