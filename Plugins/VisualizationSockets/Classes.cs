using System;
using System.Numerics;
using Newtonsoft.Json;

using ETS2LA.Game.SDK;
using ETS2LA.Game.Data;

using TruckLib;
using TruckLib.ScsMap;

namespace VisualizationSockets;

[Serializable]
public struct SocketNode
{
    public string id;
    public Vector3 position;
    public Quaternion rotation;

    public SocketNode()
    {

    }

    public SocketNode(INode node)
    {
        this.id = node.Uid.ToString();
        this.position = node.Position;
        this.rotation = node.Rotation;
    }
}

[Serializable]
public struct SocketRoad
{
    public string id;
    public string node;
    public string forwardNode;
    public float[] laneOffsetsStart = new float[0];
    public float[] laneOffsetsEnd = new float[0];
    public int leftLaneCount;
    public int rightLaneCount;

    public SocketRoad()
    {

    }

    public SocketRoad(Road road)
    {
        this.id = road.Uid.ToString();
        this.node = road.Node.Uid.ToString();
        this.forwardNode = road.ForwardNode.Uid.ToString();
        
        var parsedRoad = new ParsedRoad(road);
        this.laneOffsetsEnd = parsedRoad.LeftLaneOffsetsEnd.Concat(parsedRoad.RightLaneOffsetsEnd).ToArray();
        this.leftLaneCount = parsedRoad.GetLaneCount(Side.Left);
        this.rightLaneCount = parsedRoad.GetLaneCount(Side.Right);

        if (parsedRoad.LeftLaneOffsetsStart != null && parsedRoad.RightLaneOffsetsStart != null)
        {
            this.laneOffsetsStart = parsedRoad.LeftLaneOffsetsStart.Concat(parsedRoad.RightLaneOffsetsStart).ToArray();
        }
    }
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
    public string id;
    public Vector3 position = Vector3.Zero;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 size = Vector3.Zero;
    public List<SocketTrailer> trailers = new List<SocketTrailer>();

    public SocketVehicle()
    {

    }

    public SocketVehicle(string id, Vector3 position, Quaternion rotation, Vector3 size, List<SocketTrailer> trailers)
    {
        this.id = id;
        this.position = position;
        this.rotation = rotation;
        this.size = size;
        this.trailers = trailers;
    }

    public SocketVehicle(TrafficVehicle trafficVehicle)
    {
        this.id = trafficVehicle.id.ToString();
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
        this.id = parkedVehicle.id.ToString();
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

    public Dictionary<string, SocketNode> nodes = new Dictionary<string, SocketNode>();
    public List<SocketRoad> roads = new List<SocketRoad>();
    public List<SocketVehicle> vehicles = new List<SocketVehicle>();

    public DataFrame()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        telemetryData = new SocketTelemetryData();
        nodes = new Dictionary<string, SocketNode>();
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
    public Dictionary<string, SocketNode> nodes = new Dictionary<string, SocketNode>();
    public List<SocketRoad> roads = new List<SocketRoad>();

    public StaticDataAdditions()
    {
        nodes = new Dictionary<string, SocketNode>();
        roads = new List<SocketRoad>();
    }
}

[Serializable]
public struct StaticDataRemovals
{
    public List<string> nodes = new List<string>();
    public List<string> roads = new List<string>();

    public StaticDataRemovals()
    {
        nodes = new List<string>();
        roads = new List<string>();
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