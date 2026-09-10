using System;
using System.Numerics;
using Newtonsoft.Json;

using ETS2LA.Game.SDK;
using ETS2LA.Game.Data;
using ETS2LA.Game.PmdFiles;

using TruckLib;
using TruckLib.ScsMap;
using TruckLib.Models;

namespace VisualizationSockets;

[Serializable]
public struct SocketNode
{
    public string id;
    public Vector3 position;
    public Quaternion rotation;

    public SocketNode()
    {
        this.id = Guid.NewGuid().ToString();
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
    public float length;

    public float[] laneOffsetsStart = new float[0];
    public float[] laneOffsetsEnd = new float[0];
    public int leftLaneCount;
    public int rightLaneCount;

    public SocketRoad()
    {
        this.id = Guid.NewGuid().ToString();
        this.node = Guid.NewGuid().ToString();
        this.forwardNode = Guid.NewGuid().ToString();
    }

    public SocketRoad(Road road)
    {
        this.id = road.Uid.ToString();
        this.node = road.Node.Uid.ToString();
        this.forwardNode = road.ForwardNode.Uid.ToString();
        this.length = road.Length;
        
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
public struct SocketPrefabSegment
{
    public Vector3 startPosition;
    public Vector3 endPosition;
    public Quaternion startRotation;
    public Quaternion endRotation;

    public float length;
}

[Serializable]
public struct SocketPrefab
{
    public string id;
    public List<SocketPrefabSegment> segments = new List<SocketPrefabSegment>();

    public Vector3 prefabStart = Vector3.Zero;
    public Vector3 rootNodePosition = Vector3.Zero;
    public Vector3 prefabRotation = Vector3.Zero;

    public SocketPrefab()
    {
        this.id = Guid.NewGuid().ToString();
    }

    public SocketPrefab(Prefab prefab)
    {
        this.id = prefab.Uid.ToString();
        var parsedPrefab = new ParsedPrefab(prefab);
        if (parsedPrefab.Descriptor == null)
        {
            return;
        }

        this.segments = parsedPrefab.Descriptor.NavCurves.Select(s => new SocketPrefabSegment
        {
            startPosition = s.StartPosition,
            endPosition = s.EndPosition,
            startRotation = s.StartRotation,
            endRotation = s.EndRotation,
            length = s.Length
        }).ToList();

        int origin = prefab.Origin;
        this.prefabStart = prefab.Nodes[0].Position - parsedPrefab.Descriptor.Nodes[origin].Position;
        this.rootNodePosition = prefab.Nodes[0].Position;
        this.prefabRotation = prefab.Nodes[0].Rotation.ToEuler() - MathEx.GetNodeRotation(parsedPrefab.Descriptor.Nodes[origin].Direction).ToEuler();
    }
}

[Serializable]
public struct SocketBoundingBox
{
    public Vector3 min = Vector3.Zero;
    public Vector3 max = Vector3.Zero;
    
    public SocketBoundingBox()
    {

    }

    public SocketBoundingBox(AxisAlignedBox box)
    {
        this.min = box.Start;
        this.max = box.End;
    }
}

[Serializable]
public struct SocketModelPiece
{
    public SocketBoundingBox boundingBox = new SocketBoundingBox();
    public Vector3 boundingBoxCenter = Vector3.Zero;

    public SocketModelPiece()
    {

    }

    public SocketModelPiece(Piece piece)
    {
        this.boundingBox = new SocketBoundingBox(piece.BoundingBox);
        this.boundingBoxCenter = piece.BoundingBoxCenter;
    }
}

[Serializable]
public struct SocketModelPart
{
    public List<SocketModelPiece> pieces = new List<SocketModelPiece>();

    public SocketModelPart()
    {

    }

    public SocketModelPart(Part part)
    {
        this.pieces = part.Pieces.Select(p => new SocketModelPiece(p)).ToList();
    }
}

[Serializable]
public struct SocketModel
{
    public string id = "";
    public string node = "";
    public Vector3 scale = Vector3.One;
    public SocketBoundingBox boundingBox = new SocketBoundingBox();
    public Vector3 boundingBoxCenter = Vector3.Zero;
    public List<SocketModelPart> parts = new List<SocketModelPart>();

    public SocketModel() { }

    public SocketModel(TruckLib.ScsMap.Model model)
    {
        TruckLib.Models.Model? pmdModel = PmdFileHandler.Current.GetPmdModel(model.Name.ToString());
        if (pmdModel == null)
        {
            return;
        }

        this.id = model.Uid.ToString();
        this.node = model.Node.Uid.ToString();
        this.scale = model.Scale;
        this.boundingBox = new SocketBoundingBox(pmdModel.BoundingBox);
        this.boundingBoxCenter = pmdModel.BoundingBoxCenter;
        this.parts = pmdModel.Parts.Select(p => new SocketModelPart(p)).ToList();
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
        this.id = Guid.NewGuid().ToString();
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
    public List<SocketPrefab> prefabs = new List<SocketPrefab>();
    public List<SocketModel> models = new List<SocketModel>();

    public StaticDataAdditions()
    {
        nodes = new Dictionary<string, SocketNode>();
        roads = new List<SocketRoad>();
        prefabs = new List<SocketPrefab>();
        models = new List<SocketModel>();
    }
}

[Serializable]
public struct StaticDataRemovals
{
    public List<string> nodes = new List<string>();
    public List<string> roads = new List<string>();
    public List<string> prefabs = new List<string>();
    public List<string> models = new List<string>();

    public StaticDataRemovals()
    {
        nodes = new List<string>();
        roads = new List<string>();
        prefabs = new List<string>();
        models = new List<string>();
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