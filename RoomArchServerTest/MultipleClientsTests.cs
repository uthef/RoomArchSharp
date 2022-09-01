using NUnit.Framework;
using System.Net.WebSockets;
using RoomArch.Models;
using System.Text;

namespace RoomArchServerTest
{
    public class MultipleClientsTests
    {
        ClientWebSocket _host = new ClientWebSocket(), _guest = new ClientWebSocket();
    
        [OneTimeSetUp]
        public void RunApp()
        {
            Task.Run(() => 
                {
                    RoomArchServer.Program.DevelopmentEnvironment = true;
                    RoomArchServer.Program.Endpoint = new RoomArchServer.WebSocketEndpoint() {
                        IdleTimeout = TimeSpan.FromSeconds(5),
                        AuthorizationTimeout = TimeSpan.FromSeconds(2),
                        MaxRequestSize = 1024
                    };

                    RoomArchServer.Program.Main(Array.Empty<string>());
                }
            );
        }

        [SetUp]
        public async Task OpenWebSockets()
        {
            await _host.ConnectAsync(new Uri("ws://localhost:5000/endpoint"), CancellationToken.None);

            await _host.SendAsync(new Notification(new Credential("api-key", "1.0", "Windows")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            ArraySegment<byte> result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.AuthorizationSuccess));

            await _guest.ConnectAsync(new Uri("ws://localhost:5000/endpoint"), CancellationToken.None);
            await _guest.SendAsync(new Notification(new Credential("api-key", "1.0", "Windows")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.AuthorizationSuccess));
        }

        [TearDown]
        public async Task CloseWebSockets()
        {   
            if (_guest.State is not WebSocketState.Aborted)
                await _guest.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            if (_host.State is not WebSocketState.Aborted)
                await _host.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            
            _guest = new ClientWebSocket();
            _host = new ClientWebSocket();
        }

        public async Task<ArraySegment<byte>> ReceiveAsync(ClientWebSocket webSocket)
        {
            byte[] buffer = new byte[4096];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            return new ArraySegment<byte>(buffer, 0, result.Count);
        }

        [Test]
        public async Task BasicTest()
        {
            // Creating a room
            await _host.SendAsync(new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "user")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            ArraySegment<byte> result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomCreated));

            // Trying to create the same room from guest
            await _guest.SendAsync(new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "alex")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomNameTaken));

            // Joining the room
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));

            // Presence notification (joined)
            result = await ReceiveAsync(_host);
            Notification? notification = Notification.Deserialize(Encoding.UTF8.GetString(result));
            Assert.That(notification?.Type, Is.EqualTo("presence"));
            Assert.That(notification?.Value, Is.EqualTo("true"));
            Assert.That(notification?.Sender, Is.EqualTo("user2"));
            
            // Leaving the room
            await _guest.SendAsync(new Notification(NotificationType.LeaveRoom).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomLeft));

            // Presence notification (left)
            result = await ReceiveAsync(_host);
            notification = Notification.Deserialize(Encoding.UTF8.GetString(result));
            Assert.That(notification?.Type, Is.EqualTo("presence"));
            Assert.That(notification?.Value, Is.EqualTo("false"));
            Assert.That(notification?.Sender, Is.EqualTo("user2"));

            // Joining the room using host's name
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "USER")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.UsernameTaken));

            // Joining the room once again
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "alex")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));

            // Trying to join the same room
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "alex")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.LeaveBeforeJoining));

            // Receiving presence notification (joined)
            result = await ReceiveAsync(_host);
            notification = Notification.Deserialize(Encoding.UTF8.GetString(result));
            Assert.That(notification?.Type, Is.EqualTo("presence"));
            Assert.That(notification?.Value, Is.EqualTo("true"));
            Assert.That(notification?.Sender, Is.EqualTo("alex"));

            // Host is leaving the room
            await _host.SendAsync(new Notification(NotificationType.LeaveRoom).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomLeft));

            // Receiving kick out message
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.KickedOut));

            // Join non-existing room
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "alex")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomDoesNotExist));

        }

        [Test]
        public async Task PasswordTest()
        {
            // Creating a room
            await _host.SendAsync(new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "user") { Password = "123" }).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            ArraySegment<byte> result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomCreated));

            // Trying to join the room without a password
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.InvalidPassword));

            // Trying to join the room with a password
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2") { Password = "123" }).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));

            // Trying to modify room password not being a host
            await _guest.SendAsync(new Notification(new RoomModification() { Password = "" }).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.UnallowedRequest));

            // Leaving the room
            await _guest.SendAsync(new Notification(NotificationType.LeaveRoom).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomLeft));
        }

        [Test]
        public async Task PassDataTest()
        {
            // Creating a room
            await _host.SendAsync(new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "user")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            ArraySegment<byte> result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomCreated));

            // Joining the room
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));

            // Receiving presence notification (joined)
            result = await ReceiveAsync(_host);
            Notification? notification = Notification.Deserialize(Encoding.UTF8.GetString(result));
            Assert.That(notification?.Type, Is.EqualTo("presence"));
            Assert.That(notification?.Value, Is.EqualTo("true"));
            Assert.That(notification?.Sender, Is.EqualTo("user2"));

            await _host.SendAsync(new Notification(NotificationType.Pass, "none", "bingo").UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            result = await ReceiveAsync(_guest);
            notification = Notification.Deserialize(Encoding.UTF8.GetString(result));
            Assert.That(notification?.Value, Is.EqualTo("bingo"));
        }

        [Test]
        public async Task KickTest()
        {
            // Creating a room
            await _host.SendAsync(new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "user")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            ArraySegment<byte> result = await ReceiveAsync(_host);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomCreated));

            // Joining the room
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));

            // Kicking user2 out
            await _host.SendAsync(new Notification(NotificationType.Kick, new string[] {"user2"}).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);

            // Receiving message
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.KickedOutByHost));

            // Joining the room once again
            await _guest.SendAsync(new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user2")).UTF8Bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            result = await ReceiveAsync(_guest);
            Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomJoined));
        }
    }
}