using ETS2LA.Game.Telemetry;
using ETS2LA.Game.SDK;
using ETS2LA.Game;
using ETS2LA.State;
using ETS2LA.Shared;
using ETS2LA.Logging;
using TruckLib.ScsMap;

using System.Numerics;
using System.Diagnostics;

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

    private Stopwatch sinceLastStaticUpdate = new Stopwatch();
    private Websocket? fastServer;
    private Websocket? staticDataServer;

    private INode[] nearbyNodes = Array.Empty<INode>();    

    public override void OnEnable()
    {
        base.OnEnable();

        fastServer = new Websocket("http://localhost:37525/");
        staticDataServer = new Websocket("http://localhost:37526/");
        staticDataServer.OnClientConnected += (socket) =>
        {
            nearbyNodes = Array.Empty<INode>();
            sinceLastStaticUpdate.Start();
            Logger.Info("Reset static data due to new client.");
        };

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

        if (sinceLastStaticUpdate.ElapsedMilliseconds > 1000 && ApplicationState.Current.RunningGame != null)
        {
            sinceLastStaticUpdate.Restart();
            var mapData = ApplicationState.Current.RunningGame.GetMapData();
            if (mapData == null)
                return;
            
            Vector3Double center = GameTelemetry.Current.GetCurrentData().truckPlacement.coordinate;
            double minX = center.X - 250;
            double maxX = center.X + 250;
            double minZ = center.Z - 250;
            double maxZ = center.Z + 250;
            var nodes = mapData.Nodes.Within(minX, minZ, maxX, maxZ);

            List<INode> addedNodes = new List<INode>();
            List<INode> removedNodes = new List<INode>();
            int totalChanges = 0;
            foreach (var node in nearbyNodes)
            {
                if (totalChanges > 100)
                    break;

                if (!nodes.Contains(node))
                {
                    removedNodes.Add(node);
                    totalChanges++;
                }
            }
            foreach (var node in nodes)
            {
                if (totalChanges > 100)
                    break;
                
                if (!nearbyNodes.Contains(node))
                {
                    addedNodes.Add(node);
                    totalChanges++;
                }
            }
            
            if (addedNodes.Count == 0 && removedNodes.Count == 0)
                return;

            Logger.Info($"Sending static data update. Added nodes: {addedNodes.Count}, Removed nodes: {removedNodes.Count}");
            SendStaticData(
                new StaticDataMessage
                {
                    add = new StaticDataAdditions
                    {
                        nodes = addedNodes.ToDictionary(n => n.Uid, n => new SocketNode(n))
                    },
                    remove = new StaticDataRemovals
                    {
                        nodes = removedNodes.Select(n => n.Uid).ToList()
                    }
                }.ToJson()
            );
        }
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