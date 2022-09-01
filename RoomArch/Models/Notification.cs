using Newtonsoft.Json;
using System.Text;

namespace RoomArch.Models;

public class Notification
{  
    [JsonRequired]
    [JsonProperty("type")]
    public string Type {get; set;}

    [JsonProperty("method")]
    public string? Method {get; set;}

    [JsonProperty("sender")]
    public string? Sender {get; set;}

    [JsonProperty("cred")]
    public Credential? Credential {get; set;}

    [JsonProperty("room")]
    public RoomConfiguration? RoomConfiguration {get; set;}

    [JsonProperty("state")]
    public RoomModification? RoomModification {get; set;}

    [JsonProperty("code")]
    public NotificationCode? Code {get; set;}

    [JsonProperty("value")]
    public string? Value {get; set;}

    [JsonProperty("clients")]
    public string[]? Clients {get; set;}

    [JsonIgnore]
    public string Serialized {
        get 
        {
          return JsonConvert.SerializeObject(this, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                }
            ); 
        }
    }

    [JsonIgnore]
    public byte[] UTF8Bytes {
        get
        {
            return Encoding.UTF8.GetBytes(Serialized);
        }
    }

    [JsonConstructor]
    public Notification(string type)
    {
        Type = type;
    }

    public Notification(NotificationCode code)
    {
        Type = NotificationType.Message;
        Code = code;
    }

    public Notification(Credential credential)
    {
        Type = NotificationType.Authorization;
        Credential = credential;
    }

    public Notification(string sender, bool present)
    {
        Type = NotificationType.Presence;
        Sender = sender;
        Value = JsonConvert.SerializeObject(present);
    }
    
    public Notification(string method, object value)
    {
        Type = NotificationType.Pass;
        Method = method;
        Value = JsonConvert.SerializeObject(value);
    }

    public Notification(string sender, string method, string value)
    {
        Type = NotificationType.Pass;
        Method = method;
        Sender = sender;
        Value = value;
    }

    public Notification(string method, object value, string[] clients)
    {
        Type = NotificationType.Pass;
        Method = method;
        Clients = clients;
        Value = JsonConvert.SerializeObject(value);
    }

    public Notification(string type, string[] clients)
    {
        Type = type;
        Clients = clients;
    }

    public Notification(RoomModification modification)
    {
        Type = NotificationType.Modification;
        RoomModification = modification;
    }

    public Notification(string type, RoomConfiguration roomConfig)
    {
        Type = type;
        RoomConfiguration = roomConfig;
    }

    public static Notification? Deserialize(string json)
    {
        return JsonConvert.DeserializeObject<Notification>(json);
    }

    public static Notification? Deserialize(byte[] utf8bytes)
    {
        return JsonConvert.DeserializeObject<Notification>(Encoding.UTF8.GetString(utf8bytes));
    }
}