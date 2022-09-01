namespace RoomArchServer;

[AttributeUsage(AttributeTargets.Method)]
public class RequestHandler : Attribute
{
    public string JsonType;

    public RequestHandler(string jsonType)
    {
        JsonType = jsonType;
    }
}