using System.Net.WebSockets;
using RoomArch.Models;
using Microsoft.AspNetCore.Mvc;

namespace RoomArchServer;

public delegate void MessageEventHandler(Client client, byte[] data);
public delegate void ClientStateEventHandler(Client client);

public class WebSocketEndpoint
{
    public int BufferSize = 4096;
    public int MaxRequestSize = 4096;
    public TimeSpan AuthorizationTimeout = TimeSpan.FromSeconds(5);
    public TimeSpan IdleTimeout = TimeSpan.FromMinutes(1);
    public event MessageEventHandler? MessageReceived;
    public event ClientStateEventHandler? Connected;
    public event ClientStateEventHandler? Disconnected;
    public async Task AcceptRequest(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
            {
                Client client = new Client(webSocket);
                Connected?.Invoke(client);

                try 
                {
                    await StartPolling(client);
                }
                catch (Exception e)
                {
                    #if DEBUG
                        Console.WriteLine($"Polling exception: {e.Message}");
                        Console.WriteLine(e.StackTrace);
                    #endif
                }
                
                await CloseSafe(client.WebSocket, WebSocketCloseStatus.NormalClosure, null);
                Disconnected?.Invoke(client);
            }
        }
        else 
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    public async Task CloseSafe(WebSocket webSocket, WebSocketCloseStatus status, string? description)
    {
        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await webSocket.CloseOutputAsync(status, description, CancellationToken.None);
    }

    public static async Task SendSafe(WebSocket webSocket, byte[] data)
    {
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task StartPolling(Client client)
    {
        while (!client.WebSocket.CloseStatus.HasValue)
        {
            byte[] buffer = new byte[BufferSize];
            WebSocketReceiveResult receiveResult = await client.WebSocket.ReceiveAsync(buffer, 
                new CancellationTokenSource(client.Authorized ? IdleTimeout : AuthorizationTimeout).Token);

            byte[] bytes = new byte[receiveResult.Count];

            if (receiveResult.Count > MaxRequestSize)
            {
                await CloseSafe(client.WebSocket, WebSocketCloseStatus.MessageTooBig, ClosureMessage.MaxRequestSizeExceeded);
                break;
            }

            Buffer.BlockCopy(buffer, 0, bytes, 0, receiveResult.Count);

            while (!receiveResult.EndOfMessage)
            {
                receiveResult = await client.WebSocket.ReceiveAsync(buffer, 
                    new CancellationTokenSource().Token);
                byte[] tempBytes = new byte[bytes.Length + receiveResult.Count];

                if (tempBytes.Length > MaxRequestSize)
                {
                    await CloseSafe(client.WebSocket, WebSocketCloseStatus.MessageTooBig, ClosureMessage.MaxRequestSizeExceeded);
                    break;
                }

                Buffer.BlockCopy(bytes, 0, tempBytes, 0, bytes.Length);
                Buffer.BlockCopy(buffer, 0, tempBytes, bytes.Length, receiveResult.Count);
                
                bytes = tempBytes;
            }

            if (bytes.Length > 0) MessageReceived?.Invoke(client, bytes);
        }
    }
}