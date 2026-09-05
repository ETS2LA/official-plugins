using ETS2LA.Game.Telemetry;
using ETS2LA.Game.SDK;
using ETS2LA.State;
using ETS2LA.Shared;
using ETS2LA.Logging;

using System.Numerics;

namespace VisualizationSockets;

public class VisualizationSockets : Plugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "tumppi066.visualizationsockets",
        Name = "Visualization Sockets",
        Description = "This plugin is used to communicate with the visualization interface. It sends telemetry and map data to the visualization interface.",
        Version = "0.1.0",
        SupportedETS2LA = ">=2026.8.1",
        Icon = "https://avatars.githubusercontent.com/u/162675991?s=128",
        AuthorName = "Tumppi066",
        AuthorWebsite = "https://tumppi066.fi",
    };

    public override float TickRate => 10f;

    private Websocket? fastServer;
    private Websocket? staticDataServer;

    public override void OnEnable()
    {
        base.OnEnable();

        fastServer = new Websocket("http://localhost:37525/");
        staticDataServer = new Websocket("http://localhost:37526/");

        fastServer.Start();
        staticDataServer.Start();
    }

    public override void Tick() // TickRate = 10f, so this is called every 0.1 seconds
    {
        List<SocketVehicle> vehicles = new List<SocketVehicle>();
        
        var trafficData = TrafficProvider.Current.GetCurrentTrafficData();
        var parkedVehicles = ParkedVehiclesProvider.Current.GetCurrentParkedVehicleData();
        
        if (trafficData != null)
            vehicles.AddRange(trafficData.vehicles.Where(v => v.Position != Vector3.Zero).Select(v => new SocketVehicle(v)));
        if (parkedVehicles != null)
            vehicles.AddRange(parkedVehicles.vehicles.Where(v => v.Position != Vector3.Zero).Select(v => new SocketVehicle(v)));

        SendFastData(
            new DataFrame
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                telemetryData = new SocketTelemetryData
                {
                    position = CameraProvider.Current.GetCurrentData().truckPosition,
                    rotation = CameraProvider.Current.GetCurrentData().truckRotation
                },
                vehicles = vehicles,
            }.ToJson()
        );
    }

    public override void OnDisable()
    {
        base.OnDisable();
        fastServer?.Stop();
        staticDataServer?.Stop();
    }

    public void SendFastData(string jsonMessage)
    {
        fastServer?.Broadcast(jsonMessage);
    }

    public void SendStaticData(string jsonMessage)
    {
        staticDataServer?.Broadcast(jsonMessage);
    }
}