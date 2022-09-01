# RoomArchSharp
Room Multiplayer Architecture implementation for .NET applications.

## Modules
* RoomArch (compatible with .NET Framework 4.7.2)
* RoomArchServer (ASP.NET template, .NET 6.0)
* RoomArchServerTest (NUnit Test Project, .NET 6.0)

## Third-party packages
* RoomArch <= [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/13.0.1)

## Usage examples
For client-side usage examples, see __SingleClientTests__ and __MultipleClientsTests__ classes inside __RoomArchServerTest__ project

Server-side logic might be extended or modified inside __RoomArchServer__ project

### Binding a class with shared methods and subscribing to __RoomBinder__ basic events
~~~ cs
using RoomArch;
using System;
using System.Collections.Generic;

public class Test
{
    public void Bind()
    {
        RoomBinder binder = new RoomBinder();
        binder.AddClassWithSharedMethods(this);

        binder.MessageReceived += (code) =>
        {
            Console.WriteLine(code);
        };

        binder.ClientPresenceUpdate += (name, present) =>
        {
            Console.Write($"{name} {(present ? "joined the room" : "left the room")}");
        };
    }

    [SharedMethod]
    public void Method(string sender, List<int> values)
    {
        // Notification method name: Test.Method
        // The second parameter may be any type
    }
}
~~~

### Serializing notifications
~~~ cs
binder.PreparePassRequest(this, nameof(Method), new List<int> {1, 2, 3}); // returns UTF-8 byte array
binder.PreparePassRequest(this, nameof(Method), new List<int> {1, 2, 3}, new string[] {"user1", "user2"});

byte[] utf8bytes = new Notification(NotificationType.JoinRoom, new RoomConfiguration("room", "user")).UTF8Bytes;
~~~

### Processing raw server notification
~~~ cs
byte[] bytes;
binder.ProcessBytes(bytes); // returns true if processed and false otherwise
~~~