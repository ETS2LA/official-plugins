using ETS2LA.Game.Telemetry;

namespace VirtualDashcam;

public interface IRecordingEvent
{
    // Timestamp in events is float since the recording start.
    // Only the Recording object knows the start and end in RTC.
    public float Timestamp { get; set; }
    public string LayerName { get; }
    public float ApproxTimeTillNext { get; set; }
}

[Serializable]
public class PlayerEvent: IRecordingEvent
{
    public float Timestamp { get; set; }
    public string LayerName { get; } = "Player";
    public float ApproxTimeTillNext { get; set; }

    public DataFrame Data;
}

[Serializable]
public class TrafficEvent: IRecordingEvent
{
    public float Timestamp { get; set; }
    public string LayerName { get; } = "Traffic";
    public float ApproxTimeTillNext { get; set; }

    public List<SocketVehicle> Vehicles;
}

[Serializable]
public class Recording
{
    public DateTime StartTime;
    public DateTime EndTime;
    public string Username = String.Empty;

    public List<IRecordingEvent> Events = new();

    public void Save(string? prefix = "Recording")
    {
        if (Events.Count == 0)
        {
            return;
        }

        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ETS2LA", "Recordings");
        Directory.CreateDirectory(basePath);
        RecordingSerializer.Save(this, Path.Combine(basePath, $"{prefix}_{Username}_{StartTime:yyyyMMdd_HHmmss}.vdcr"));
    }

    public void SavePastHalfMinute()
    {
        if (Events.Count == 0)
        {
            return;
        }

        var newRecording = new Recording
        {
            StartTime = DateTime.UtcNow.AddSeconds(-30),
            EndTime = DateTime.UtcNow,
            Username = Username
        };

        var currentTimestamp = Events.Last().Timestamp;
        foreach (var ev in Events)
        {
            if (ev.Timestamp >= currentTimestamp - 30)
            {
                newRecording.Events.Add(ev);
            }
        }
        
        newRecording.Save("Collision_Event");
        newRecording.Events.Clear();
    }
}

public class WearChecker
{
    private float wearEngine;
    private float wearTransmission;
    private float wearCabin;
    private float wearChassis;
    private float wearWheels;

    public WearChecker(GameTelemetryData data)
    {
        SetFromData(data);
    }

    private void SetFromData(GameTelemetryData data)
    {
        wearEngine = data.truckFloat.wearEngine;
        wearTransmission = data.truckFloat.wearTransmission;
        wearCabin = data.truckFloat.wearCabin;
        wearChassis = data.truckFloat.wearChassis;
        wearWheels = data.truckFloat.wearWheels;
    }

    public bool HasWearChanged(GameTelemetryData data)
    {
        if (data == null)
        {
            return false;
        }

        var engineDiff = Math.Abs(wearEngine - data.truckFloat.wearEngine);
        var transmissionDiff = Math.Abs(wearTransmission - data.truckFloat.wearTransmission);
        var cabinDiff = Math.Abs(wearCabin - data.truckFloat.wearCabin);
        var chassisDiff = Math.Abs(wearChassis - data.truckFloat.wearChassis);
        var wheelsDiff = Math.Abs(wearWheels - data.truckFloat.wearWheels);
        var maxDiff = Math.Max(Math.Max(Math.Max(Math.Max(engineDiff, transmissionDiff), cabinDiff), chassisDiff), wheelsDiff);
        SetFromData(data);

        if (maxDiff > 0.1f) {
            return true;
        }

        return false;
    }
}