namespace RoomArchServer.Models;

public class RoomServerConfiguration
{
    public string[] ApiKeys {get; set;} = {};
    public string[] SupportedVersions {get; set;} = {};
    public int MaxRequestSize {get; set;} = 4096;
    public int BufferSize {get; set;} = 4096;
}