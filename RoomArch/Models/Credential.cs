using Newtonsoft.Json;

namespace RoomArch.Models;

public class Credential
{
    [JsonRequired]
    [JsonProperty("key")]
    public string ApiKey;

    [JsonRequired]
    [JsonProperty("ver")]
    public string RoomVersion;

    [JsonRequired]
    [JsonProperty("os")]
    public string OS;

    [JsonConstructor]
    public Credential(string apiKey, string version, string os)
    {
        ApiKey = apiKey;
        RoomVersion = version;
        OS = os;
    }
}