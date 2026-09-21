using ETS2LA.Game.Telemetry;
using ETS2LA.Game.SDK;
using ETS2LA.Game;
using ETS2LA.Notifications;
using ETS2LA.Backend;
using ETS2LA.Game.Data;
using ETS2LA.Backend.Events;
using ETS2LA.State;
using ETS2LA.Shared;
using ETS2LA.Logging;
using ETS2LA.Game.PmdFiles;

using PathLib;
using TruckLib;
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
        Description = "This plugin is used to communicate with the visualization interface.",
        Version = "1.0.0",
        SupportedETS2LA = ">=2026.9.5026",
        Icon = "https://avatars.githubusercontent.com/u/162675991?s=128",
        AuthorName = "Tumppi066",
        AuthorWebsite = "https://tumppi066.fi",
        Dependencies = {
            "tumppi066.pathlib"
        }
    };

    public override float TickRate => 10f;

    private Stopwatch sinceLastStaticUpdate = new Stopwatch();
    private Websocket? fastServer;
    private Websocket? staticDataServer;
    private bool staticNeedsToSendMoreData = false;

    private HashSet<ulong> sentNodes = new();
    private HashSet<ulong> sentRoads = new();
    private HashSet<ulong> sentPrefabs = new();
    private HashSet<ulong> sentModels = new();

    private PlannedPathData? pathData;
    private BaseVehicle? leadingVehicle;
    private ParsedSemaphore? targetSemaphore;
    private bool didSubscribeToData = false;

    public override void OnEnable()
    {
        base.OnEnable();
        GameTelemetry.Current.ReadTrailerData = true;

        fastServer = new Websocket("http://localhost:37525/");
        staticDataServer = new Websocket("http://localhost:37526/");
        staticDataServer.OnClientConnected += (socket) =>
        {
            sentNodes.Clear();
            sentRoads.Clear();
            sentPrefabs.Clear();
            sentModels.Clear();
            sinceLastStaticUpdate.Restart();

            Logger.Info("Reset static data due to new client.");
            NotificationHandler.Current.SendNotification(
                new Notification
                {
                    Id = "VisualizationSockets.StaticDataReset",
                    Title = "Visualization Sockets",
                    Content = "New client connection, resetting static data...",
                    Level = NotificationLevel.Information,
                    CloseAfter = 3f
                }
            );
        };

        if (!didSubscribeToData)
        {
            Events.Current.Subscribe<PlannedPathData>("Pathfinding.PlannedPathData", data => { pathData = data; });
            Events.Current.Subscribe<BaseVehicle?>("AdaptiveCruiseControl.LeadingVehicle", data => { leadingVehicle = data; });
            Events.Current.Subscribe<ParsedSemaphore?>("AdaptiveCruiseControl.TargetParsedSemaphore", data => { targetSemaphore = data; });
            didSubscribeToData = true;
        }

        fastServer.Start();
        staticDataServer.Start();
    }

    public override void Tick()
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
                    position = GameTelemetry.Current.GetCurrentData().truckPlacement.coordinate.ToVector3(),
                    rotation = CameraProvider.Current.GetCurrentData().truckRotation,

                    trailers = GameTelemetry.Current.GetCurrentData().trailers.Where(t => t.comBool.attached).Select(t => new SocketTelemetryTrailer
                    {
                        position = t.comDouble.worldPosition,
                        rotationEuler = t.comDouble.worldRotation,
                        hookPosition = t.comVector.hookPosition,
                        wheels = t.comVector.wheelPositions.ToList(),
                    }).ToList(),

                    speed = GameTelemetry.Current.GetCurrentData().truckFloat.speed,
                    speedLimit = GameTelemetry.Current.GetCurrentData().truckFloat.speedLimit,
                    throttle = GameTelemetry.Current.GetCurrentData().truckFloat.gameThrottle,
                    brake = GameTelemetry.Current.GetCurrentData().truckFloat.gameBrake,
                    clutch = GameTelemetry.Current.GetCurrentData().truckFloat.gameClutch,
                    steering = GameTelemetry.Current.GetCurrentData().truckFloat.gameSteer,
                },
                selfDrivingData = new SocketSelfDrivingData
                {
                    pathPoints = GetPathPoints(),
                    targetVehicles = [leadingVehicle is TrafficVehicle trafficVehicle ? trafficVehicle.id : leadingVehicle is TrafficTrailer trafficTrailer ? trafficTrailer.parent.id : -1],
                    // TODO: ParsedSemaphore is the wrong one, edit ACC to send the right one
                    targetSemaphores = [targetSemaphore is ParsedSemaphore parsedSemaphore ? (int)parsedSemaphore.Semaphore.SemaphoreId : -1],
                    targetSpeed = ApplicationState.Current.DesiredSpeed,
                    isControllingSteering = ApplicationState.Current.DrivingMode >= DrivingMode.FullSelfDriving && ApplicationState.Current.EnableAssists
                                            && PluginBackend.Current.PluginHandler?.LoadedPlugins.Any(p => p.Info.Id == "tumppi066.laneassist") == true,
                    isControllingAcceleration = ApplicationState.Current.DrivingMode <= DrivingMode.FullSelfDriving && ApplicationState.Current.EnableAssists
                                                && PluginBackend.Current.PluginHandler?.LoadedPlugins.Any(p => p.Info.Id == "tumppi066.adaptivecruisecontrol") == true
                },
                vehicles = vehicles,
            }.ToJson()
        );

        if ((sinceLastStaticUpdate.ElapsedMilliseconds > 1000 || staticNeedsToSendMoreData) && ApplicationState.Current.RunningGame != null)
        {
            UpdateStaticData();
            sinceLastStaticUpdate.Restart();
        }
    }

    private List<Vector3> GetPathPoints()
    {
        if (pathData == null || pathData.HasError)
            return new List<Vector3>();

        Vector3 truckPos = CameraProvider.Current.GetCurrentData().truckPosition;
        float steeringPointDistance = 1.5f * Math.Max(1f, GameTelemetry.Current.GetCurrentData().truckFloat.speed / 25f * 3.6f);

        float closestFactor = pathData.GetFactorForPoint(truckPos);
        float closestDist = closestFactor * pathData.TotalLength;
        List<OrientedPoint> steeringPoints = new List<OrientedPoint>();
        for (int i = 0; i < 20; i++)
        {
            try
            {
                OrientedPoint? point = pathData.InterpolateDist(closestDist + i * steeringPointDistance, affectLaneChange: i == 0);
                if (point != null) {
                    steeringPoints.Add(point.Value);
                }
            } 
            catch {} 
        }

        return steeringPoints.Select(p => p.Position).ToList();
    }

    private void UpdateStaticData()
    {
        if (ApplicationState.Current.RunningGame == null)
            return;

        var mapData = ApplicationState.Current.RunningGame.GetMapData();
        if (mapData == null)
            return;

        Vector3Double center =
            GameTelemetry.Current
                .GetCurrentData()
                .truckPlacement
                .coordinate;

        const double VIEW_DISTANCE = 512;

        double minX = center.X - VIEW_DISTANCE;
        double maxX = center.X + VIEW_DISTANCE;
        double minZ = center.Z - VIEW_DISTANCE;
        double maxZ = center.Z + VIEW_DISTANCE;

        var nodes = mapData.Nodes.Within(
                        minX,
                        minZ,
                        maxX,
                        maxZ
                    );

        var desiredNodes = new Dictionary<ulong, INode>();
        var desiredRoads = new Dictionary<ulong, Road>();
        var desiredModels = new Dictionary<ulong, Model>();
        var desiredPrefabs = new Dictionary<ulong, Prefab>();
        foreach (var node in nodes)
        {
            switch (node.ForwardItem)
            {
                case Road road:
                    desiredRoads[road.Uid] = road;
                    desiredNodes[road.Node.Uid] = road.Node;
                    desiredNodes[road.ForwardNode.Uid] = road.ForwardNode;
                    break;
                case Prefab prefab:
                    desiredPrefabs[prefab.Uid] = prefab;
                    break;
                // TODO: Optimize model loading, this lags ETS2LA for ~20 seconds at first start
                // case Model model:
                //     desiredModels[model.Uid] = model;
                //     desiredNodes[model.Node.Uid] = model.Node;
                //     break;
            }
        }

        var addedNodes = desiredNodes
            .Where(x => !sentNodes.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();

        var addedRoads = desiredRoads
            .Where(x => !sentRoads.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();

        var addedPrefabs = desiredPrefabs
            .Where(x => !sentPrefabs.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();

        var addedModels = desiredModels
            .Where(x => !sentModels.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();

        var removedNodes = sentNodes
            .Where(id => !desiredNodes.ContainsKey(id))
            .ToList();

        var removedRoads = sentRoads
            .Where(id => !desiredRoads.ContainsKey(id))
            .ToList();

        var removedPrefabs = sentPrefabs
            .Where(id => !desiredPrefabs.ContainsKey(id))
            .ToList();

        var removedModels = sentModels
            .Where(id => !desiredModels.ContainsKey(id))
            .ToList();

        if (
            addedNodes.Count == 0 &&
            addedRoads.Count == 0 &&
            addedPrefabs.Count == 0 &&
            addedModels.Count == 0 &&
            removedNodes.Count == 0 &&
            removedRoads.Count == 0 &&
            removedPrefabs.Count == 0 &&
            removedModels.Count == 0
        ) return;

        SendStaticData(
            new StaticDataMessage
            {
                add = new StaticDataAdditions
                {
                    nodes = addedNodes.ToDictionary(n => n.Uid.ToString(), n => new SocketNode(n)),
                    roads = addedRoads.Select(r => new SocketRoad(r)).ToList(),
                    prefabs = addedPrefabs.Select(p => new SocketPrefab(p)).ToList(),
                    models = addedModels.Select(m => new SocketModel(m)).ToList()
                },

                remove = new StaticDataRemovals
                {
                    nodes = removedNodes.Select(id => id.ToString()).ToList(),
                    roads = removedRoads.Select(id => id.ToString()).ToList(),
                    prefabs = removedPrefabs.Select(id => id.ToString()).ToList(),
                    models = removedModels.Select(id => id.ToString()).ToList()
                }
            }.ToJson()
        );

        sentNodes = desiredNodes.Keys.ToHashSet();
        sentRoads = desiredRoads.Keys.ToHashSet();
        sentPrefabs = desiredPrefabs.Keys.ToHashSet();
        sentModels = desiredModels.Keys.ToHashSet();
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