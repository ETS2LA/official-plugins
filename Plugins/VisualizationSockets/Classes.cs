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

    public SocketNode()
    {

    }

    public SocketNode(INode node)
    {
        this.id = node.Uid;
        this.position = node.Position;
        this.rotation = node.Rotation;
    }
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
public struct SocketTrailer
{
    public Vector3 position = Vector3.Zero;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 size = Vector3.Zero;

    public SocketTrailer()
    {

    }

    public SocketTrailer(BaseVehicle trailer)
    {
        this.position = trailer.Position;
        this.rotation = trailer.Rotation;
        this.size = trailer.Size;
    }
}

[Serializable]
public struct SocketVehicle
{
    public ulong id;
    public Vector3 position = Vector3.Zero;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 size = Vector3.Zero;
    public List<SocketTrailer> trailers = new List<SocketTrailer>();

    public SocketVehicle()
    {

    }

    public SocketVehicle(ulong id, Vector3 position, Quaternion rotation, Vector3 size, List<SocketTrailer> trailers)
    {
        this.id = id;
        this.position = position;
        this.rotation = rotation;
        this.size = size;
        this.trailers = trailers;
    }

    public SocketVehicle(TrafficVehicle trafficVehicle)
    {
        this.id = (ulong)trafficVehicle.id;
        this.position = trafficVehicle.Position;
        this.rotation = trafficVehicle.Rotation;
        this.size = trafficVehicle.Size;

        foreach (var trailer in trafficVehicle.trailers)
        {
            if (trailer.Position == Vector3.Zero)
                continue;
            this.trailers.Add(new SocketTrailer(trailer));
        }
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

[Serializable]
public struct StaticDataAdditions
{
    public Dictionary<ulong, SocketNode> nodes = new Dictionary<ulong, SocketNode>();
    public List<SocketRoad> roads = new List<SocketRoad>();

    public StaticDataAdditions()
    {
        nodes = new Dictionary<ulong, SocketNode>();
        roads = new List<SocketRoad>();
    }
}

[Serializable]
public struct StaticDataRemovals
{
    public List<ulong> nodes = new List<ulong>();
    public List<ulong> roads = new List<ulong>();

    public StaticDataRemovals()
    {
        nodes = new List<ulong>();
        roads = new List<ulong>();
    }
}

[Serializable]
public struct StaticDataMessage
{
    public StaticDataAdditions add = new StaticDataAdditions();
    public StaticDataRemovals remove = new StaticDataRemovals();
    
    public StaticDataMessage()
    {
        add = new StaticDataAdditions();
        remove = new StaticDataRemovals();
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this);
    }
}