using Newtonsoft.Json;

namespace RoomArch.Models;

public class RoomConfiguration
{
    [JsonRequired]
    [JsonProperty("name")]
    public string Name;

    [JsonRequired]
    [JsonProperty("sender")]
    public string Sender;

    [JsonProperty("pass")]
    public string? Password;

    public RoomConfiguration(string name, string sender)
    {
        Name = name;
        Sender = sender;
    }
}