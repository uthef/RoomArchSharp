using System.Net.WebSockets;

namespace RoomArch.Models;

public class Client
{
    public readonly WebSocket WebSocket;
    public bool Authorized {get; private set;}
    public string Name {get; set;} = "";
    public string NormalizedName {get => Name.ToLower().Trim();}
    private Room? _room;
    public Room? Room
    {
        get 
        {
            return _room;
        }
        set
        {
            Room?.RemoveClient(this);
            
            if (value is not null)
            {
                if (!value.HasClient(this)) value.AddClient(this);
            }

            _room = value;
        }
    }
    public Client(WebSocket webSocket)
    {
        WebSocket = webSocket;
    }

    public void Authorize() => Authorized = true;
}