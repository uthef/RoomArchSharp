using System;
using System.Reflection;
using Newtonsoft.Json;
using RoomArch.Models;

namespace RoomArch;

public delegate void NotificationHandler(Notification notification);
public delegate void MessageEventHandler(NotificationCode code);
public delegate void ClientEventHandler(string sender, bool present);

public class RoomBinder
{
    public delegate void SharedMethodDelegate<T>(Func<T> method);
    private Dictionary<string, SharedMethodInfo> _sharedMethods = new Dictionary<string, SharedMethodInfo>();
    private Dictionary<string, NotificationHandler> _defaultHandlers = new Dictionary<string, NotificationHandler>();

    public event MessageEventHandler? MessageReceived;
    public event ClientEventHandler? ClientPresenceUpdate;

    public RoomBinder()
    {
        _defaultHandlers[NotificationType.Message] = Message;
        _defaultHandlers[NotificationType.Pass] = Pass;
        _defaultHandlers[NotificationType.Presence] = Presence;
    }

    public void AddClassWithSharedMethods<T>(T @class) where T : class
    {
        IEnumerable<MethodInfo> methods = @class.GetType().GetRuntimeMethods()
            .Where(m => m.GetCustomAttribute<SharedMethod>() is not null && m.GetParameters().Length == 2);
        string className = @class.GetType().Name;

        foreach (MethodInfo method in methods)
        {
            ParameterInfo[] parameters = method.GetParameters();

            if (parameters[0].ParameterType != typeof(string))
            {
                throw new Exception("First shared method parameter must always be of string type");
            }

            _sharedMethods.Add($"{className}.{method.Name}", new SharedMethodInfo(@class, parameters[1].ParameterType, method));
        }
    }

    private void Pass(Notification notification)
    {
        SharedMethodInfo sharedMethod = _sharedMethods[notification?.Method!];
        object? value = JsonConvert.DeserializeObject(notification?.Value!, sharedMethod.ValueType);

        if (value is not null)
            sharedMethod.Method.Invoke(sharedMethod.ClassInstance, new object[] {notification?.Sender!, value});
    }

    private void Message(Notification notification)
    {
        if (notification.Code is not null)
            MessageReceived?.Invoke((NotificationCode) notification.Code);
    }

    private void Presence(Notification notification)
    {
        if (notification.Value is not null && notification.Sender is not null)
            ClientPresenceUpdate?.Invoke(notification.Sender, JsonConvert.DeserializeObject<bool>(notification.Value));
    }

    public byte[] PreparePassRequest<T>(T @class, string methodName, object value) where T : class
    {
        string method = $"{@class.GetType().Name}.{methodName}";
        return new Notification(method, value).UTF8Bytes;
    }

    public byte[] PreparePassRequest<T>(T @class, string methodName, object value, string[] clients) where T : class
    {
        string method = $"{@class.GetType().Name}.{methodName}";
        return new Notification(method, value, clients).UTF8Bytes;
    }

    public bool ProcessBytes(byte[] data)
    {
        try
        {
            Notification? notification = Notification.Deserialize(data);
            _defaultHandlers[notification!.Type].Invoke(notification);
            return true;
        }
        catch
        {
            return false;
        }
    }
}