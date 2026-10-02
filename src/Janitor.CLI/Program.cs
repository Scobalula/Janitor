using Janitor.Factory;
using RedFox.GameExtraction.CommandLine;

namespace Janitor.CLI;

internal static class Program
{
    private static Task<int> Main(string[] args) => GameExtractionCommandLineApp.RunAsync(new GameExtractionCommandLineConfig
    {
        AssetManagerFactory = JanitorAssetManagerFactory.Create,
        Settings = JanitorSettings.CreateDefaults(),
        SettingDefinitions = JanitorSettings.Definitions,
        Title = "Janitor",
        Description = "An asset extractor for Northlight Engine games.",
        AppName = "Janitor",
        Version = "1.0.0",
        Theme = new CommandLineTheme
        {
            Accent = "#4AC7EE",
        },
        Commands = [new NamesCommand()],
    }, args);
}
