using RoomArchServer.Models;
using EnvironmentName = Microsoft.Extensions.Hosting.EnvironmentName;

namespace RoomArchServer;

public class Program
{
    public static WebSocketEndpoint? Endpoint;
    public static bool DevelopmentEnvironment = false;
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
        {
            Args = args,
            EnvironmentName = DevelopmentEnvironment ? EnvironmentName.Development : EnvironmentName.Production
        });

        RoomServerConfiguration? roomServerConfig = 
            builder.Configuration.GetSection("RoomServerConfiguration").Get<RoomServerConfiguration>();
        
        if (roomServerConfig is null) throw new Exception("Room Server configuration is invalid");

        if (Endpoint is null)
            Endpoint = new WebSocketEndpoint()
            {
                MaxRequestSize = roomServerConfig.MaxRequestSize,
                BufferSize = roomServerConfig.BufferSize
            };
            
        RoomController roomController = new RoomController(Endpoint, roomServerConfig);

        var app = builder.Build();
        app.UseWebSockets();
        app.MapGet("/endpoint", Endpoint.AcceptRequest);
        app.Run();   
    }
}
