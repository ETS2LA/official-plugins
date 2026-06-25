using ETS2LA.Shared;

namespace ExampleLibrary;

public class ExampleLibrary : LibraryPlugin
{
    public override PluginInformation Info => new PluginInformation
    {
        Id = "developer.library",
        Version = "1.0.0",
        Name = "ExampleLibrary",
        Description = "Consectetur mollit ipsum velit Lorem fugiat aliqua officia exercitation exercitation.",
        AuthorName = "Developer",
    };
}

public struct AnExampleStruct
{
    public int ExampleInt { get; set; }
    public string ExampleString { get; set; }
}