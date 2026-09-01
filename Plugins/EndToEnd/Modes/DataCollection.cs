using System.Numerics;

using ETS2LA.Game.Telemetry;
using ETS2LA.Game.SDK;
using ETS2LA.ML.Vision;
using StbImageWriteSharp;
using System.Diagnostics;
using ETS2LA.Logging;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace EndToEnd;

public struct EndToEndNavigationData
{
    public int laneChange;
}

[Serializable]
public struct EndToEndDataEntry
{
    public double timestamp;
    public int frame;
    public GameTelemetryData telemetry;
    public EndToEndNavigationData navigation;
    public TrafficVehicle[] nearbyVehicles;
    public ETS2LA.Game.SDK.Semaphore[] nearbySemaphores;
}

public class DataCollector
{
    public bool Collecting { get; private set; } = false;
    public Stopwatch Stopwatch { get; private set; } = new Stopwatch();

    private string datasetName = string.Empty;
    private int datasetSize = 0;
    private Stopwatch below0SpeedStopwatch = new Stopwatch();

    private string dataRootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ETS2LA", "EndToEndData");
    private int collectionIntervalMs = 100;

    public void SaveCameras()
    {
        var cameras = VisionHandler.Current.Cameras;
        var writer = new ImageWriter();

        foreach (var camera in cameras)
        {
            unsafe
            {
                int width = camera.Width;
                int height = camera.Height;
                byte[] pixelData = camera.GetPixelData();

                string cameraFolder = Path.Combine(dataRootFolder, datasetName, camera.Name);
                Directory.CreateDirectory(cameraFolder);
                string imagePath = Path.Combine(cameraFolder, $"frame_{datasetSize:D6}.png");

                fixed (byte* pixelDataPtr = pixelData)
                {
                    using (var stream = File.OpenWrite(imagePath))
                    {
                        writer.WritePng(
                            pixelDataPtr, 
                            width, 
                            height, 
                            ColorComponents.RedGreenBlueAlpha, 
                            stream
                        );
                    }
                }
            }
        }
    }

    public bool CollectData()
    {
        var telemetry = GameTelemetry.Current.GetCurrentData();
        if (telemetry == null)
        {
            Stopwatch.Stop();
            return false;
        }

        if (telemetry.paused)
        {
            Stopwatch.Stop();
            return false;
        }

        if (telemetry.truckFloat.speed < 0.1f)
        {
            if (!below0SpeedStopwatch.IsRunning)
            {
                below0SpeedStopwatch.Start();
            }
            else if (below0SpeedStopwatch.Elapsed.TotalSeconds > 3)
            {
                Stopwatch.Stop();
                return false;
            }
        }
        else
        {
            below0SpeedStopwatch.Reset();
        }

        if (telemetry.truckFloat.engineRpm < 500f)
        {
            Stopwatch.Stop();
            return false;
        }

        if (!Stopwatch.IsRunning)
            Stopwatch.Start();

        var nearbyVehicles = TrafficProvider.Current.GetCurrentTrafficData()?.vehicles;
        if (nearbyVehicles != null)
        {
            nearbyVehicles = nearbyVehicles
                .Where(v => v.Position != Vector3.Zero).ToArray();
        }

        var nearbySemaphores = SemaphoreProvider.Current.GetCurrentData()?.semaphores;
        if (nearbySemaphores != null)
        {
            nearbySemaphores = nearbySemaphores
                .Where(s => s.position != Vector3.Zero).ToArray();
        }

        var entry = new EndToEndDataEntry
        {
            timestamp = Stopwatch.Elapsed.TotalSeconds,
            frame = datasetSize,
            telemetry = telemetry,
            navigation = new EndToEndNavigationData
            {
                laneChange = 0 // Placeholder for lane change data
            },
            nearbyVehicles = nearbyVehicles ?? Array.Empty<TrafficVehicle>(),
            nearbySemaphores = nearbySemaphores ?? Array.Empty<ETS2LA.Game.SDK.Semaphore>()
        };
        SaveCameras();
        SaveDataEntry(entry);
        return true;
    }

    public void SaveDataEntry(EndToEndDataEntry dataEntry)
    {
        string datasetFolder = Path.Combine(dataRootFolder, datasetName, "Dataset");
        Directory.CreateDirectory(datasetFolder);

        string datasetFilePath = Path.Combine(datasetFolder, $"data_entry_{datasetSize:D6}.json");
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            IncludeFields = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        Logger.Info($"Saving data entry {datasetSize} to {datasetFilePath}");
        File.WriteAllText(datasetFilePath, JsonSerializer.Serialize(dataEntry, options));
    }

    public void StartCollection()
    {
        if (!Collecting)
        {
            Collecting = true;
            datasetSize = 0;
            datasetName = $"dataset_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            string datasetFolder = Path.Combine(dataRootFolder, datasetName);
            Directory.CreateDirectory(datasetFolder);
            
            Stopwatch.Restart();
            var nextCollectionTime = Stopwatch.ElapsedMilliseconds + collectionIntervalMs;

            Task.Run(() =>
            {
                while (Collecting)
                {
                    if (CollectData())
                    {
                        datasetSize++;
                    }

                    if (!Stopwatch.IsRunning)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    while (Stopwatch.ElapsedMilliseconds < nextCollectionTime)
                    {
                        Thread.SpinWait(5);
                    }
                    
                    nextCollectionTime += collectionIntervalMs;
                }
            });
        }
    }

    public void StopCollection()
    {
        if (Collecting)
        {
            Collecting = false;
            Stopwatch.Stop();
        }
    }
}