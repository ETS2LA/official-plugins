using ETS2LA.Settings;

namespace EndToEnd;

[Serializable]
public enum EndToEndPluginMode
{
    DataCollection,
    Inference
}

[Serializable]
public class EndToEndSettings
{
    [NonSerialized]
    private static readonly Lazy<EndToEndSettings> _instance = new(() => new EndToEndSettings(loadSettings: true));
    public static EndToEndSettings Current => _instance.Value;


    public EndToEndPluginMode PluginMode { get; set; } = EndToEndPluginMode.DataCollection;


    [NonSerialized]
    private SettingsHandler? _settingsHandler;

    public EndToEndSettings(bool loadSettings = false)
    {
        if (loadSettings)
        {
            _settingsHandler = new SettingsHandler();
            var loadedSettings = _settingsHandler.Load<EndToEndSettings>("tumppi066.endtoend.json");
            if (loadedSettings != null)
            {

            }
        }
    }

    public EndToEndSettings() { }

    public void Save()
    {
        _settingsHandler?.Save<EndToEndSettings>("tumppi066.endtoend.json", this);
    }
}