using ETS2LA.Shared;
using ETS2LA.Game.SDK;
using ETS2LA.Game.Data;
using ETS2LA.Game.Telemetry;
using ETS2LA.Backend.Events;
using ETS2LA.State;
using ETS2LA.Backend;
using ETS2LA.Networking.Settings;
using ETS2LA.Controls;
using ETS2LA.Notifications;
using ETS2LA.Audio;
using static ETS2LA.Translations.T;
using PathLib;
using TruckLib;

using Hexa.NET.GLFW;
using System.Numerics;
using System.Diagnostics;

namespace VirtualDashcam;

public class VirtualDashcam : Plugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "tumppi066.virtualdashcam",
        Name = "Virtual Dashcam",
        Description = "This plugin can be used to record what's happening around you, without even recording your display.",
        Version = "1.0.0",
        AppVersionSupport = { LowerBound="2026.11.5250" },
        Icon = "https://avatars.githubusercontent.com/u/162675991?s=128",
        AuthorName = "Tumppi066",
        AuthorWebsite = "https://github.com/ETS2LA/official-plugins",
        DependsOn =
        {
            new PluginDependency { Id="tumppi066.pathlib" }
        }
    };

    public ControlDefinition ToggleRecording { get; } = new ControlDefinition
    {
        Id = "VirtualDashcam.ToggleRecording",
        // TRANSLATORS: This is the name of a keybind.
        Name = _("Toggle Recording"),
        Description = _("Toggles the recording state. You use this key to both start and stop recording."),
        DefaultKeybind = "Backslash",
        Type = ControlType.Boolean
    };

    public override float TickRate => 5f; // This becomes the data rate of the core
                                          // DataFrame (player telemetry data).

    public bool IsRecording;
    private Recording? ActiveRecording;
    private Stopwatch SinceStart;
    private WearChecker WearChecker;

    private PlannedPathData? pathData;
    private BaseVehicle? leadingVehicle;
    private ParsedSemaphore? targetSemaphore;
    private bool didSubscribeToData = false;

    public VirtualDashcam()
    {
        SinceStart = new Stopwatch();
        ControlsBackend.Current.RegisterControl(ToggleRecording);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        GameTelemetry.Current.ReadTrailerData = true;

        if (!didSubscribeToData)
        {
            Events.Current.Subscribe<PlannedPathData>("Pathfinding.PlannedPathData", data => { pathData = data; });
            Events.Current.Subscribe<BaseVehicle?>("AdaptiveCruiseControl.LeadingVehicle", data => { leadingVehicle = data; });
            Events.Current.Subscribe<ParsedSemaphore?>("AdaptiveCruiseControl.TargetParsedSemaphore", data => { targetSemaphore = data; });
            didSubscribeToData = true;
        }

        ControlsBackend.Current.On(ToggleRecording.Id, (s, e) =>
        {
            if (!this._IsRunning)
                return;
            
            if (e.NewValue is bool b && b)
            {
                IsRecording = !IsRecording;
                NotificationHandler.Current.SendNotification(new Notification
                {
                    Id = "VirtualDashcam.RecordingStateChanged",
                    Title = IsRecording ? _("Recording started.") : _("Recording stopped."),
                    Content = IsRecording ? _("Virtual Dashcam is now recording.") : _("Virtual Dashcam is no longer recording."),
                    CloseAfter = 3,
                });

                switch (IsRecording)
                {
                    case true:
                        AudioHandler.Current.Play(DefaultAudio.PreAlert);
                        break;
                    case false:
                        AudioHandler.Current.Play(DefaultAudio.SoftWarning);
                        break;
                }
            }
        });
    }

    public override void Tick()
    {
        if(!IsRecording)
        {
            if (ActiveRecording != null)
            {
                SinceStart.Stop();
                ActiveRecording.EndTime = DateTime.UtcNow;
                ActiveRecording.Save();
                ActiveRecording = null;
            }
            return;
        }
        else if (ActiveRecording == null) 
        {
            SinceStart.Reset();
            WearChecker = new WearChecker(GameTelemetry.Current.GetCurrentData());
            ActiveRecording = new() {
                StartTime = DateTime.UtcNow,
                Username = NetworkingSettings.Current.CurrentUser?.Username ?? "Unknown"
            };
            SinceStart.Start();
        }

        if (WearChecker.HasWearChanged(GameTelemetry.Current.GetCurrentData()))
        {
            AudioHandler.Current.Play(DefaultAudio.Warning);
            ActiveRecording.SavePastHalfMinute();
            NotificationHandler.Current.SendNotification(new Notification
            {
                Id = "VirtualDashcam.WearChanged",
                Title = _("Collision Detected"),
                Content = _("We've saved the past 30 seconds of recording."),
                CloseAfter = 5,
            });
        }

        var frame = GetDataFrame();
        var vehicles = frame.vehicles;
        frame.vehicles = new();

        ActiveRecording.Events.Add(new PlayerEvent
        {
            Timestamp = (float)SinceStart.Elapsed.TotalSeconds,
            ApproxTimeTillNext = 1 / TickRate,
            Data = frame
        });

        ActiveRecording.Events.Add(new TrafficEvent
        {
            Timestamp = (float)SinceStart.Elapsed.TotalSeconds,
            ApproxTimeTillNext = 1 / TickRate,
            Vehicles = vehicles
        });
    }

    public DataFrame GetDataFrame()
    {
        List<SocketVehicle> vehicles = new List<SocketVehicle>();
        
        var trafficData = TrafficProvider.Current.GetCurrentTrafficData();
        var parkedVehicles = ParkedVehiclesProvider.Current.GetCurrentParkedVehicleData();
        
        if (trafficData != null)
            vehicles.AddRange(trafficData.vehicles.Where(v => v.Position != Vector3.Zero).Select(v => new SocketVehicle(v)));
        if (parkedVehicles != null)
            vehicles.AddRange(parkedVehicles.vehicles.Where(v => v.Position != Vector3.Zero).Select(v => new SocketVehicle(v)));

        var frame = new DataFrame
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
        };

        return frame;
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
}
