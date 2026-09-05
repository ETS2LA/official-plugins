using System;
using System.Numerics;
using ETS2LA.Game.SDK;
using TruckLib;
using TruckLib.ScsMap;
using Newtonsoft.Json;

namespace VisualizationSockets;

[Serializable]
public struct SocketNode
{
    public ulong id;
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public struct SocketRoad
{
    public ulong id;
    public SocketNode node;
    public SocketNode forwardNode;
    public float[] laneOffsets;
    public int leftLaneCount;
    public int rightLaneCount;
}

[Serializable]
public struct SocketVehicle
{
    public ulong id;
    public Vector3 position = Vector3.Zero;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 size = Vector3.Zero;

    public SocketVehicle()
    {

    }

    public SocketVehicle(ulong id, Vector3 position, Quaternion rotation, Vector3 size)
    {
        this.id = id;
        this.position = position;
        this.rotation = rotation;
        this.size = size;
    }

    public SocketVehicle(TrafficVehicle trafficVehicle)
    {
        this.id = (ulong)trafficVehicle.id;
        this.position = trafficVehicle.Position;
        this.rotation = trafficVehicle.Rotation;
        this.size = trafficVehicle.Size;
    }

    public SocketVehicle(ParkedVehicle parkedVehicle)
    {
        this.id = (ulong)parkedVehicle.id;
        this.position = parkedVehicle.Position;
        this.rotation = parkedVehicle.Rotation;
        this.size = parkedVehicle.Size;
    }
}

[Serializable]
public struct SocketTelemetryData
{
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public struct DataFrame
{
    public long timestamp;

    public SocketTelemetryData telemetryData;

    public Dictionary<ulong, SocketNode> nodes = new Dictionary<ulong, SocketNode>();
    public List<SocketRoad> roads = new List<SocketRoad>();
    public List<SocketVehicle> vehicles = new List<SocketVehicle>();

    public DataFrame()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        telemetryData = new SocketTelemetryData();
        nodes = new Dictionary<ulong, SocketNode>();
        roads = new List<SocketRoad>();
        vehicles = new List<SocketVehicle>();
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this);
    }
}