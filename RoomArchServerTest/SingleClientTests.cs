using NUnit.Framework;
using System.Net.WebSockets;
using RoomArch.Models;
using System.Text;

namespace RoomArchServerTest;

public class SingleClientTests
{
    ClientWebSocket _webSocket;
    
    public SingleClientTests()
    {
        _webSocket = new ClientWebSocket();
    }

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
    public async Task OpenWebSocket()
    {
        await _webSocket.ConnectAsync(new Uri("ws://localhost:5000/endpoint"), CancellationToken.None);
    }

    [TearDown]
    public async Task CloseWebSocket()
    {   
        if (_webSocket.State is not WebSocketState.Aborted)
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        _webSocket = new ClientWebSocket();
    }

    public async Task<ArraySegment<byte>> ReceiveAsync()
    {
        byte[] buffer = new byte[4096];
        WebSocketReceiveResult result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
        return new ArraySegment<byte>(buffer, 0, result.Count);
    }

    [Test]
    public async Task BigRequestTest()
    {
        byte[] bytes = new byte[1025];
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await ReceiveAsync();
        Assert.That(_webSocket.CloseStatusDescription, Is.EqualTo(ClosureMessage.MaxRequestSizeExceeded));
    }

    [Test]
    [TestCase("abcdefg")]
    [TestCase("{}")]
    [TestCase("{\"type\":\"auth\",\"cred\":{}}")]
    [TestCase("{\"type\":\"none\"}")]
    public async Task InvalidRequestTest(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await ReceiveAsync();
        Assert.That(_webSocket.CloseStatusDescription, Is.EqualTo(ClosureMessage.InvalidRequest));
    }

    [Test]
    public async Task InvalidCredentialTest()
    {
        byte[] bytes = new Notification(new Credential("invalid_api_key", "1.0", "Windows")).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await ReceiveAsync();
        Assert.That(_webSocket.CloseStatusDescription, Is.EqualTo(ClosureMessage.InvalidApiKey));
    }

    [Test]
    public async Task AuthorizationTest()
    {
        byte[] bytes = new Notification(new Credential("api-key", "1.0", "Windows")).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.AuthorizationSuccess));
    }

    [Test]
    [Timeout(6000)]
    public async Task AuthorizedTimeout()
    {
        await AuthorizationTest();
        Assert.ThrowsAsync(typeof(WebSocketException), async Task () => await ReceiveAsync());
    }

    [Test]
    [Timeout(3000)]
    public void UnauthorizedTimeout()
    {
        Assert.ThrowsAsync(typeof(WebSocketException), async Task () => await ReceiveAsync());
    }
    
    [Test]
    public async Task UnauthorizedRoomCreationTest()
    {
        byte[] bytes = new Notification(NotificationType.CreateRoom, new RoomConfiguration("room", "user")).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await ReceiveAsync();
        Assert.That(_webSocket.CloseStatusDescription, Is.EqualTo(ClosureMessage.UnauthorizedAccess));
    }

    [Test]
    public async Task EmptyRoomConfigurationRequestTest()
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.CreateRoom).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomConfigurationNotSpecified));
    }

    [Test]
    [TestCase("", "")]
    [TestCase("  ", "")]
    [TestCase("abcdefghijklmnopqrstuvwxyz", "")]
    public async Task InvalidRoomNameTest(string room, string sender)
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.CreateRoom, new RoomConfiguration(room, sender)).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.InvalidRoomName));
    }

    [Test]
    [TestCase("room", "")]
    [TestCase("room", "         ")]
    [TestCase("room", "abcdefghijklmnopqrstuvwxyz")]
    public async Task InvalidUsernameTest(string room, string sender)
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.CreateRoom, new RoomConfiguration(room, sender)).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.InvalidUsername));
    }

    [TestCase("room", "user")]
    public async Task RoomAdditionTest(string room, string sender)
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.CreateRoom, new RoomConfiguration(room, sender)).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomCreated));
        bytes = new Notification(NotificationType.CreateRoom, new RoomConfiguration(room, sender)).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.LeaveBeforeCreating));
    }

    [TestCase("room", "user")]
    public async Task LeavingWhenNotInARoomTest(string room, string sender)
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.LeaveRoom).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.NoRoomToLeave));
    }

    [TestCase("room", "user")]
    public async Task JoiningNonExistingRoomTest(string room, string sender)
    {
        await AuthorizationTest();
        byte[] bytes = new Notification(NotificationType.JoinRoom, new RoomConfiguration(room, sender)).UTF8Bytes;
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        ArraySegment<byte> result = await ReceiveAsync();
        Assert.That(Notification.Deserialize(Encoding.UTF8.GetString(result))?.Code, Is.EqualTo(NotificationCode.RoomDoesNotExist));
    }
}