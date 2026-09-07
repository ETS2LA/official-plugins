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
    private bool staticNeedsToSendMoreData = false;

    private HashSet<ulong> nearbyNodes = new HashSet<ulong>();
    private HashSet<ulong> nearbyRoads = new HashSet<ulong>();

    public override void OnEnable()
    {
        base.OnEnable();

        fastServer = new Websocket("http://localhost:37525/");
        staticDataServer = new Websocket("http://localhost:37526/");
        staticDataServer.OnClientConnected += (socket) =>
        {
            nearbyNodes.Clear();
            nearbyRoads.Clear();
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

        if ((sinceLastStaticUpdate.ElapsedMilliseconds > 1000 || staticNeedsToSendMoreData) && ApplicationState.Current.RunningGame != null)
        {
            sinceLastStaticUpdate.Restart();
            var mapData = ApplicationState.Current.RunningGame.GetMapData();
            if (mapData == null)
                return;
            
            Vector3Double center = GameTelemetry.Current.GetCurrentData().truckPlacement.coordinate;
            double minX = center.X - 512;
            double maxX = center.X + 512;
            double minZ = center.Z - 512;
            double maxZ = center.Z + 512;
            var nodes = mapData.Nodes.Within(minX, minZ, maxX, maxZ);

            List<INode> addedNodes = new List<INode>();
            List<INode> removedNodes = new List<INode>();
            List<Road> addedRoads = new List<Road>();
            List<Road> removedRoads = new List<Road>();

            int totalChanges = 0;

            // Removing nodes that are no longer nearby
            foreach (var node in nearbyNodes)
            {
                if (totalChanges > 100)
                    break;

                if (!nodes.Any(n => n.Uid == node))
                {
                    removedNodes.Add(mapData.Nodes[node]);
                    nearbyNodes.Remove(node);
                    totalChanges++;
                    if (mapData.Nodes[node].ForwardItem is Road forwardRoad)
                    {
                        if (nearbyRoads.Contains(forwardRoad.Uid))
                        {
                            nearbyRoads.Remove(forwardRoad.Uid);
                            removedRoads.Add(forwardRoad);
                        }
                    }
                    if (mapData.Nodes[node].BackwardItem is Road backwardRoad)
                    {
                        if (nearbyRoads.Contains(backwardRoad.Uid))
                        {
                            nearbyRoads.Remove(backwardRoad.Uid);
                            removedRoads.Add(backwardRoad);
                        }
                    }
                }
            }

            // Adding new nodes that are now nearby
            foreach (var node in nodes)
            {
                if (totalChanges > 100)
                    break;
                
                if (!nearbyNodes.Contains(node.Uid))
                {
                    addedNodes.Add(node);
                    nearbyNodes.Add(node.Uid);
                    totalChanges++;

                    // Check items for this current node
                    var front = node.ForwardItem;
                    var back = node.BackwardItem;
                    if (front != null && front is Road forwardRoad)
                    {
                        if (!nearbyRoads.Contains(forwardRoad.Uid))
                        {
                            nearbyRoads.Add(forwardRoad.Uid);
                            addedRoads.Add(forwardRoad);

                            // Check this road's nodes in case they are not already added
                            if (!nearbyNodes.Contains(forwardRoad.Node.Uid))
                            {
                                addedNodes.Add(forwardRoad.Node);
                                nearbyNodes.Add(forwardRoad.Node.Uid);
                            }
                            if (!nearbyNodes.Contains(forwardRoad.ForwardNode.Uid))
                            {
                                addedNodes.Add(forwardRoad.ForwardNode);
                                nearbyNodes.Add(forwardRoad.ForwardNode.Uid);
                            }
                        }
                    }
                    if (back != null && back is Road backwardRoad)
                    {
                        if (!nearbyRoads.Contains(backwardRoad.Uid))
                        {
                            nearbyRoads.Add(backwardRoad.Uid);
                            addedRoads.Add(backwardRoad);

                            // Check this road's nodes in case they are not already added
                            if (!nearbyNodes.Contains(backwardRoad.Node.Uid))
                            {
                                addedNodes.Add(backwardRoad.Node);
                                nearbyNodes.Add(backwardRoad.Node.Uid);
                            }
                            if (!nearbyNodes.Contains(backwardRoad.ForwardNode.Uid))
                            {
                                addedNodes.Add(backwardRoad.ForwardNode);
                                nearbyNodes.Add(backwardRoad.ForwardNode.Uid);
                            }
                        }
                    }
                }
            }
            
            if (addedNodes.Count == 0 && removedNodes.Count == 0 
             && addedRoads.Count == 0 && removedRoads.Count == 0)
                return;

            if (totalChanges > 100)
                staticNeedsToSendMoreData = true;
            else 
                staticNeedsToSendMoreData = false;

            Logger.Info($"Sending static data update:\nNodes added: {addedNodes.Count}, removed: {removedNodes.Count}\nRoads added: {addedRoads.Count}, removed: {removedRoads.Count}");
            SendStaticData(
                new StaticDataMessage
                {
                    add = new StaticDataAdditions
                    {
                        nodes = addedNodes.ToDictionary(n => n.Uid.ToString(), n => new SocketNode(n)),
                        roads = addedRoads.Select(r => new SocketRoad(r)).ToList()
                    },
                    remove = new StaticDataRemovals
                    {
                        nodes = removedNodes.Select(n => n.Uid.ToString()).ToList(),
                        roads = removedRoads.Select(r => r.Uid.ToString()).ToList()
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