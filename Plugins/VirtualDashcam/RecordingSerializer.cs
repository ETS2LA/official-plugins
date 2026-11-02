using System.IO.Compression;
using System.Numerics;

namespace VirtualDashcam;

// TODO: This file was AI generated. The rest of this plugin is made by me,
//       but I did not write my own serializer. Eventually I might take a look
//       and make an optimized one etc, but for now this is fine.

// Binary serializer for Recording objects.
//
// JSON was taking megabytes per 10 seconds because every Vector3/Quaternion
// became a verbose object and every vehicle id was repeated as a string on
// every frame. This format writes raw little-endian primitives and then GZip
// compresses the whole stream, which is both far smaller and much faster.
//
// Format (after GZip decompression, all little-endian):
//   uint32  magic "VDCR"
//   byte    version
//   int64   startTime (UTC ticks)
//   int64   endTime   (UTC ticks)
//   string  username
//   int32   eventCount
//   event[] events
//
// Event:
//   byte    type (0 = Player, 1 = Traffic)
//   float   timestamp
//   float   approxTimeTillNext
//   <payload>  (DataFrame for Player, List<SocketVehicle> for Traffic)
//
// Strings are written with BinaryWriter's 7-bit length prefix + UTF8.
public static class RecordingSerializer
{
    private const uint Magic = 0x52434456; // "VDCR" little-endian
    private const byte Version = 1;

    private const byte EventPlayer = 0;
    private const byte EventTraffic = 1;

    public static void Save(Recording recording, string path)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        using var writer = new BinaryWriter(gzip);

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(recording.StartTime.Ticks);
        writer.Write(recording.EndTime.Ticks);
        writer.Write(recording.Username ?? string.Empty);

        writer.Write(recording.Events.Count);
        foreach (var evt in recording.Events)
        {
            switch (evt)
            {
                case PlayerEvent player:
                    writer.Write(EventPlayer);
                    writer.Write(player.Timestamp);
                    writer.Write(player.ApproxTimeTillNext);
                    WriteDataFrame(writer, player.Data);
                    break;
                case TrafficEvent traffic:
                    writer.Write(EventTraffic);
                    writer.Write(traffic.Timestamp);
                    writer.Write(traffic.ApproxTimeTillNext);
                    WriteVehicles(writer, traffic.Vehicles);
                    break;
                default:
                    throw new NotSupportedException($"Unknown recording event type: {evt.GetType().FullName}");
            }
        }
    }

    public static Recording Load(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);

        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("Not a Virtual Dashcam recording.");

        var version = reader.ReadByte();
        if (version != Version)
            throw new InvalidDataException($"Unsupported recording version {version}.");

        var recording = new Recording
        {
            StartTime = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
            EndTime = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
            Username = reader.ReadString(),
        };

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            byte type = reader.ReadByte();
            float timestamp = reader.ReadSingle();
            float approx = reader.ReadSingle();

            switch (type)
            {
                case EventPlayer:
                    recording.Events.Add(new PlayerEvent
                    {
                        Timestamp = timestamp,
                        ApproxTimeTillNext = approx,
                        Data = ReadDataFrame(reader),
                    });
                    break;
                case EventTraffic:
                    recording.Events.Add(new TrafficEvent
                    {
                        Timestamp = timestamp,
                        ApproxTimeTillNext = approx,
                        Vehicles = ReadVehicles(reader),
                    });
                    break;
                default:
                    throw new InvalidDataException($"Unknown event type {type}.");
            }
        }

        return recording;
    }

    // ---- DataFrame -------------------------------------------------------

    private static void WriteDataFrame(BinaryWriter w, DataFrame frame)
    {
        w.Write(frame.timestamp);
        WriteTelemetryData(w, frame.telemetryData);
        WriteSelfDrivingData(w, frame.selfDrivingData);

        var nodes = frame.nodes;
        w.Write(nodes?.Count ?? 0);
        if (nodes != null)
        {
            foreach (var kvp in nodes)
            {
                w.Write(kvp.Key);
                WriteNode(w, kvp.Value);
            }
        }

        WriteRoads(w, frame.roads);
        WriteVehicles(w, frame.vehicles);
    }

    private static DataFrame ReadDataFrame(BinaryReader r)
    {
        var frame = new DataFrame
        {
            timestamp = r.ReadInt64(),
            telemetryData = ReadTelemetryData(r),
            selfDrivingData = ReadSelfDrivingData(r),
        };

        int nodeCount = r.ReadInt32();
        for (int i = 0; i < nodeCount; i++)
        {
            var key = r.ReadString();
            frame.nodes[key] = ReadNode(r);
        }

        frame.roads = ReadRoads(r);
        frame.vehicles = ReadVehicles(r);
        return frame;
    }

    private static void WriteTelemetryData(BinaryWriter w, SocketTelemetryData data)
    {
        WriteVector3(w, data.position);
        WriteQuaternion(w, data.rotation);

        var trailers = data.trailers;
        w.Write(trailers?.Count ?? 0);
        if (trailers != null)
        {
            foreach (var trailer in trailers)
                WriteTelemetryTrailer(w, trailer);
        }

        w.Write(data.speed);
        w.Write(data.speedLimit);
        w.Write(data.throttle);
        w.Write(data.brake);
        w.Write(data.clutch);
        w.Write(data.steering);
    }

    private static SocketTelemetryData ReadTelemetryData(BinaryReader r)
    {
        var data = new SocketTelemetryData
        {
            position = ReadVector3(r),
            rotation = ReadQuaternion(r),
            trailers = new List<SocketTelemetryTrailer>(),
        };

        int trailerCount = r.ReadInt32();
        for (int i = 0; i < trailerCount; i++)
            data.trailers.Add(ReadTelemetryTrailer(r));

        data.speed = r.ReadSingle();
        data.speedLimit = r.ReadSingle();
        data.throttle = r.ReadSingle();
        data.brake = r.ReadSingle();
        data.clutch = r.ReadSingle();
        data.steering = r.ReadSingle();
        return data;
    }

    private static void WriteTelemetryTrailer(BinaryWriter w, SocketTelemetryTrailer trailer)
    {
        WriteVector3(w, trailer.position);
        WriteVector3(w, trailer.rotationEuler);
        WriteVector3(w, trailer.hookPosition);
        WriteVector3List(w, trailer.wheels);
    }

    private static SocketTelemetryTrailer ReadTelemetryTrailer(BinaryReader r)
    {
        return new SocketTelemetryTrailer
        {
            position = ReadVector3(r),
            rotationEuler = ReadVector3(r),
            hookPosition = ReadVector3(r),
            wheels = ReadVector3List(r),
        };
    }

    private static void WriteSelfDrivingData(BinaryWriter w, SocketSelfDrivingData data)
    {
        WriteVector3List(w, data.pathPoints);
        WriteIntList(w, data.targetVehicles);
        WriteIntList(w, data.targetSemaphores);
        w.Write(data.targetSpeed);
        w.Write(data.isControllingSteering);
        w.Write(data.isControllingAcceleration);
    }

    private static SocketSelfDrivingData ReadSelfDrivingData(BinaryReader r)
    {
        return new SocketSelfDrivingData
        {
            pathPoints = ReadVector3List(r),
            targetVehicles = ReadIntList(r),
            targetSemaphores = ReadIntList(r),
            targetSpeed = r.ReadSingle(),
            isControllingSteering = r.ReadBoolean(),
            isControllingAcceleration = r.ReadBoolean(),
        };
    }

    private static void WriteNode(BinaryWriter w, SocketNode node)
    {
        w.Write(node.id ?? string.Empty);
        WriteVector3(w, node.position);
        WriteQuaternion(w, node.rotation);
    }

    private static SocketNode ReadNode(BinaryReader r)
    {
        return new SocketNode
        {
            id = r.ReadString(),
            position = ReadVector3(r),
            rotation = ReadQuaternion(r),
        };
    }

    private static void WriteRoads(BinaryWriter w, List<SocketRoad>? roads)
    {
        w.Write(roads?.Count ?? 0);
        if (roads == null)
            return;

        foreach (var road in roads)
        {
            w.Write(road.id ?? string.Empty);
            w.Write(road.node ?? string.Empty);
            w.Write(road.forwardNode ?? string.Empty);
            w.Write(road.length);
            WriteFloatArray(w, road.laneOffsetsStart);
            WriteFloatArray(w, road.laneOffsetsEnd);
            w.Write(road.leftLaneCount);
            w.Write(road.rightLaneCount);
        }
    }

    private static List<SocketRoad> ReadRoads(BinaryReader r)
    {
        int count = r.ReadInt32();
        var roads = new List<SocketRoad>(count);
        for (int i = 0; i < count; i++)
        {
            roads.Add(new SocketRoad
            {
                id = r.ReadString(),
                node = r.ReadString(),
                forwardNode = r.ReadString(),
                length = r.ReadSingle(),
                laneOffsetsStart = ReadFloatArray(r),
                laneOffsetsEnd = ReadFloatArray(r),
                leftLaneCount = r.ReadInt32(),
                rightLaneCount = r.ReadInt32(),
            });
        }
        return roads;
    }

    private static void WriteVehicles(BinaryWriter w, List<SocketVehicle>? vehicles)
    {
        w.Write(vehicles?.Count ?? 0);
        if (vehicles == null)
            return;

        foreach (var vehicle in vehicles)
        {
            w.Write(vehicle.id ?? string.Empty);
            w.Write(vehicle.speed);
            WriteVector3(w, vehicle.position);
            WriteQuaternion(w, vehicle.rotation);
            WriteVector3(w, vehicle.size);

            var trailers = vehicle.trailers;
            w.Write(trailers?.Count ?? 0);
            if (trailers != null)
            {
                foreach (var trailer in trailers)
                {
                    WriteVector3(w, trailer.position);
                    WriteQuaternion(w, trailer.rotation);
                    WriteVector3(w, trailer.size);
                }
            }
        }
    }

    private static List<SocketVehicle> ReadVehicles(BinaryReader r)
    {
        int count = r.ReadInt32();
        var vehicles = new List<SocketVehicle>(count);
        for (int i = 0; i < count; i++)
        {
            var vehicle = new SocketVehicle
            {
                id = r.ReadString(),
                speed = r.ReadSingle(),
                position = ReadVector3(r),
                rotation = ReadQuaternion(r),
                size = ReadVector3(r),
                trailers = new List<SocketTrailer>(),
            };

            int trailerCount = r.ReadInt32();
            for (int j = 0; j < trailerCount; j++)
            {
                vehicle.trailers.Add(new SocketTrailer
                {
                    position = ReadVector3(r),
                    rotation = ReadQuaternion(r),
                    size = ReadVector3(r),
                });
            }

            vehicles.Add(vehicle);
        }
        return vehicles;
    }

    // ---- Primitives ------------------------------------------------------

    private static void WriteVector3(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }

    private static Vector3 ReadVector3(BinaryReader r)
    {
        return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    private static void WriteQuaternion(BinaryWriter w, Quaternion q)
    {
        w.Write(q.X);
        w.Write(q.Y);
        w.Write(q.Z);
        w.Write(q.W);
    }

    private static Quaternion ReadQuaternion(BinaryReader r)
    {
        return new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    private static void WriteVector3List(BinaryWriter w, List<Vector3>? list)
    {
        w.Write(list?.Count ?? 0);
        if (list == null)
            return;

        foreach (var v in list)
            WriteVector3(w, v);
    }

    private static List<Vector3> ReadVector3List(BinaryReader r)
    {
        int count = r.ReadInt32();
        var list = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
            list.Add(ReadVector3(r));
        return list;
    }

    private static void WriteIntList(BinaryWriter w, List<int>? list)
    {
        w.Write(list?.Count ?? 0);
        if (list == null)
            return;

        foreach (var i in list)
            w.Write(i);
    }

    private static List<int> ReadIntList(BinaryReader r)
    {
        int count = r.ReadInt32();
        var list = new List<int>(count);
        for (int i = 0; i < count; i++)
            list.Add(r.ReadInt32());
        return list;
    }

    private static void WriteFloatArray(BinaryWriter w, float[]? array)
    {
        w.Write(array?.Length ?? 0);
        if (array == null)
            return;

        foreach (var f in array)
            w.Write(f);
    }

    private static float[] ReadFloatArray(BinaryReader r)
    {
        int count = r.ReadInt32();
        var array = new float[count];
        for (int i = 0; i < count; i++)
            array[i] = r.ReadSingle();
        return array;
    }
}
