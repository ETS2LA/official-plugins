using ETS2LA.Game.Telemetry;
using ETS2LA.State;

using ETS2LA.Shared;

namespace EndToEnd;

public class EndToEnd : Plugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "tumppi066.endtoend",
        Name = "End-To-End",
        Description = "This plugin implements an end-to-end driving model for ETS2LA. It uses a neural network to predict the next steering points.",
        Version = "0.1.0",
        SupportedETS2LA = ">=2026.8.1",
        Icon = "https://avatars.githubusercontent.com/u/162675991?s=128",
        AuthorName = "Tumppi066",
        AuthorWebsite = "https://tumppi066.fi",
    };

    DataCollector dataCollector = new DataCollector();

    public override void Init()
    {
        base.Init();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        if (EndToEndSettings.Current.PluginMode == EndToEndPluginMode.DataCollection)
        {
            dataCollector.StartCollection();
        }
    }

    public override void Tick()
    {
        
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (EndToEndSettings.Current.PluginMode == EndToEndPluginMode.DataCollection)
        {
            dataCollector.StopCollection();
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
    }
}
