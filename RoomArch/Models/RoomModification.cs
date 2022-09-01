using Newtonsoft.Json;

namespace RoomArch.Models;

public class RoomModification
{
    [JsonProperty("lock")]
    public bool? Locked;

    [JsonProperty("limit")]
    public int? Limit;

    [JsonProperty("pass")]
    public string? Password;
}