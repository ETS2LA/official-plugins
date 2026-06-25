using ETS2LA.Shared;
using ExampleLibrary;
using ETS2LA.Logging;

namespace ExamplePlugin;

public class ExamplePlugin : Plugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "developer.plugin",
        Version = "1.0.0",
        Name = "ExamplePlugin",
        Description = "Consectetur mollit ipsum velit Lorem fugiat aliqua officia exercitation exercitation.",
        AuthorName = "Developer",
        Dependencies = new List<string>
        {
            "developer.library" // ExampleLibrary in Libraries/ExampleLibrary
        }
    };

    public override void Init()
    {
        base.Init();
        // This is run once when the plugin is initially loaded.
        // Usually you start to listen to control events here (or register your own).
        // ControlHandler.Current.On(ControlHandler.Defaults.Next.Id, OnNextPressed);

        // This comes from the ExampleLibrary. This is how you share classes between
        // your plugins. YOU CANNOT JUST IMPORT A PLUGIN!!!
        // It has to be a library plugin due to the way ETS2LA's plugin system works.
        var exampleStruct = new AnExampleStruct
        {
            ExampleInt = 42,
            ExampleString = "Hello, World!"
        };
        Logger.Info($"ExampleStruct: ExampleInt = {exampleStruct.ExampleInt}, ExampleString = {exampleStruct.ExampleString}");
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // Subscribe to events here, do not subscribe in Init as that's too early.
        // Events.Current.Subscribe<YourEventType>("YourTopic", YourEventHandler);
    }

    public override void Tick()
    {
        // This will be run at the specified TickRate when the plugin is enabled.
        // You can use `public override float TickRate => 10f;` to change the tick rate to whatever TPS you want.
        // Please do not use a tick rate higher than what you need, that uses extra performance. ie. something related to
        // steering might not need over 20 TPS, something related to speed control doesn't need over 10 TPS etc...

        // If your code works exclusively off of events you can remove this override. If this is the case you should make
        // sure to lower the tickrate to something low, 1 TPS or even 0.1 TPS is usually fine. The Tick can be used as a general
        // timer thread in these situations.
    }
    
    public override void OnDisable()
    {
        base.OnDisable();
        // Unsubscribe from events here
        // Events.Current.Unsubscribe<YourEventType>("YourTopic", YourEventHandler);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        // This is run once when the plugin is unloaded (at app shutdown), use it to clean up any resources or
        // threads you created in Init or elsewhere.
    }
}
