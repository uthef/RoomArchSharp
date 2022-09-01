using RoomArchServer.Models;
using RoomArch.Models;
using System.Reflection;
using System.Net.WebSockets;

namespace RoomArchServer;

public delegate void RequestHandlerDelegate(Client client, Notification notification);

public class RoomController 
{
    public readonly WebSocketEndpoint Endpoint;
    public readonly RoomServerConfiguration Configuration;
    private readonly Dictionary<string, RequestHandlerDelegate> _handlers = new Dictionary<string, RequestHandlerDelegate>();
    private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
    public const int MaxNameLength = 16;
    public int MaxClientAmount = 5;
    public RoomController(WebSocketEndpoint endpoint, RoomServerConfiguration configuration)
    {
        Endpoint = endpoint;
        Configuration = configuration;

        Endpoint.MessageReceived += MessageReceived;
        Endpoint.Connected += ClientConnected;
        Endpoint.Disconnected += ClientDisconnected;

        IEnumerable<MethodInfo> methods = GetType().GetRuntimeMethods();

        foreach (MethodInfo method in methods)
        {
            if (method.GetCustomAttribute<RequestHandler>() is RequestHandler attr)
            {
                RequestHandlerDelegate handler = (RequestHandlerDelegate) Delegate.CreateDelegate(typeof(RequestHandlerDelegate), this, method);
                _handlers.Add(attr.JsonType, handler);
            }
        }
    }

    private async void MessageReceived(Client client, byte[] data)
    {
        try 
        {
            Notification? notification = Notification.Deserialize(data);
            
            if (notification is not null)
                _handlers[notification.Type].Invoke(client, notification);
        }
        catch (Exception e)
        {
            #if DEBUG
                Console.WriteLine($"Message parsing exception: {e.Message}");
            #endif 
            await Endpoint.CloseSafe(client.WebSocket, System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation, ClosureMessage.InvalidRequest);
        }
    }

    private void ClientConnected(Client client)
    {
        #if DEBUG
            Console.WriteLine("Client has connected");
        #endif
    }

    private void ClientDisconnected(Client client)
    {
        #if DEBUG
            Console.WriteLine("Client has been disconnected");
        #endif

        if (client.Room is not null)
        {
            if (client.Room.Host == client) _rooms.Remove(client.Room.NormalizedName);
            client.Room = null;
        }
    }

    private async Task<bool> Filter(Client client)
    {
        if (client.Authorized)
        {
            return true;
        }
        else
        {
            await Endpoint.CloseSafe(client.WebSocket, WebSocketCloseStatus.PolicyViolation, ClosureMessage.UnauthorizedAccess);
            return false;
        }
    }

    private async Task<bool> ValidateRoomRequest(Client client, Notification notification)
    {
        if (notification.RoomConfiguration is null)
        {
            await WebSocketEndpoint.SendSafe(client.WebSocket, 
                new Notification(NotificationCode.RoomConfigurationNotSpecified).UTF8Bytes);
            return false;
        }

        if (notification.RoomConfiguration.Name.Trim().Length is 0 or > MaxNameLength)
        {
            await WebSocketEndpoint.SendSafe(client.WebSocket, 
                new Notification(NotificationCode.InvalidRoomName).UTF8Bytes);
            return false;
        }

        if (notification.RoomConfiguration.Sender.Trim().Length is 0 or > MaxNameLength)
        {
            await WebSocketEndpoint.SendSafe(client.WebSocket, 
                new Notification(NotificationCode.InvalidUsername).UTF8Bytes);
            return false;
        }

        return true;
    }

    [RequestHandler(NotificationType.Authorization)]
    private async void AuthorizationRequest(Client client, Notification notification)
    {
        if (client.Authorized) return;

        if (notification.Credential is null)
        {
            await Endpoint.CloseSafe(client.WebSocket, WebSocketCloseStatus.PolicyViolation, ClosureMessage.InvalidCredential);
            return;
        }

        if (!Configuration.ApiKeys.Contains(notification.Credential.ApiKey))
        {
            await Endpoint.CloseSafe(client.WebSocket, WebSocketCloseStatus.PolicyViolation, ClosureMessage.InvalidApiKey);
            return;
        }

        if (!Configuration.SupportedVersions.Contains(notification.Credential.RoomVersion))
        {
            await Endpoint.CloseSafe(client.WebSocket, WebSocketCloseStatus.PolicyViolation, ClosureMessage.UnsupportedVersion);
            return;
        }

        client.Authorize();
        await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.AuthorizationSuccess).UTF8Bytes);
    }

    [RequestHandler(NotificationType.CreateRoom)]
    private async void RoomCreationRequest(Client client, Notification notification)
    {
        if (await Filter(client) && await ValidateRoomRequest(client, notification))
        {
            string roomName = notification.RoomConfiguration!.Name.ToLower().Trim();

            if (client.Room is not null)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.LeaveBeforeCreating).UTF8Bytes);
                return;
            }

            if (_rooms.ContainsKey(roomName))
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomNameTaken).UTF8Bytes);
                return;
            }

            client.Name = notification.RoomConfiguration.Sender;
            Room room = new Room(notification.RoomConfiguration, client);
            room.ClientLimit = MaxClientAmount;
            client.Room = room;
            _rooms.Add(roomName, room);

            await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomCreated).UTF8Bytes);
        }
    }

    [RequestHandler(NotificationType.JoinRoom)]
    private async void RoomJoiningRequest(Client client, Notification notification)
    {
        if (await Filter(client) && await ValidateRoomRequest(client, notification))
        {
            string roomName = notification.RoomConfiguration!.Name.ToLower().Trim();

            if (client.Room is not null)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.LeaveBeforeJoining).UTF8Bytes);
                return;
            }

            if (!_rooms.ContainsKey(roomName))
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomDoesNotExist).UTF8Bytes);
                return;
            }

            Room room = _rooms[roomName];
            
            if (room.Count == room.ClientLimit)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.ClientLimitReached).UTF8Bytes);
                return;
            }

            if (room.Locked)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomLocked).UTF8Bytes);
                return;
            }

            if (room.HasClient(notification.RoomConfiguration.Sender.ToLower().Trim()))
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.UsernameTaken).UTF8Bytes);
                return;
            }

            if (room.Password is not null && room.Password.Length != 0 && room.Password != notification.RoomConfiguration.Password)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.InvalidPassword).UTF8Bytes);
                return;
            }

            client.Name = notification.RoomConfiguration.Sender;
            client.Room = room;
            await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomJoined).UTF8Bytes);
        }
    }

    [RequestHandler(NotificationType.LeaveRoom)]
    private async void LeaveRequest(Client client, Notification notification)
    {
        if (await Filter(client))
        {
            if (client.Room is not null)
            {
                if (client.Room.Host == client) _rooms.Remove(client.Room.NormalizedName);
                client.Room = null;
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomLeft).UTF8Bytes);
            }
            else
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.NoRoomToLeave).UTF8Bytes);
            }
        }
    }

    [RequestHandler(NotificationType.Modification)]
    private async void RoomModificationRequest(Client client, Notification notification)
    {
        if (await Filter(client))
        {
            if (notification.RoomModification is null)
            {
                await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.RoomModificationNotSpecified).UTF8Bytes);
                return;
            }

            if (client.Room is not null && client.Room.Host == client)
            {
                client.Room.Locked = notification.RoomModification.Locked is null ? client.Room.Locked : (bool) notification.RoomModification.Locked;

                if (notification.RoomModification.Limit is not null and not 0 && notification.RoomModification.Limit <= MaxClientAmount)
                    client.Room.ClientLimit = (int) notification.RoomModification.Limit;

                if (notification.RoomModification.Password is not null)
                    client.Room.Password = notification.RoomModification.Password;
                
            }
            else await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.UnallowedRequest).UTF8Bytes);
        }
    }

    [RequestHandler(NotificationType.Kick)]
    private async void KickRequest(Client client, Notification notification)
    {
        if (await Filter(client) && notification.Clients is not null)
        {
            if (client.Room is not null && client.Room.Host == client)
            {
                await client.Room.RemoveClients(client, new Notification(NotificationCode.KickedOutByHost), notification.Clients);
            }
            else await WebSocketEndpoint.SendSafe(client.WebSocket, new Notification(NotificationCode.UnallowedRequest).UTF8Bytes);
        }
    }

    [RequestHandler(NotificationType.Pass)]
    private async void PassRequest(Client client, Notification notification)
    {
        if (await Filter(client) && notification.Value is not null && notification.Method is not null)
        {
            if (notification.Clients is null)
                client.Room?.Broadcast(client, new Notification(client.Name, notification.Method, notification.Value));
            else
                client.Room?.Broadcast(client, new Notification(client.Name, notification.Method, notification.Value), notification.Clients);
        }
    }
}