using Janitor.Factory;
using RedFox.GameExtraction;
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
        Version = JanitorBuildInfo.DisplayVersion,
        Theme = new CommandLineTheme
        {
            Accent = "#4AC7EE",
        },
        Commands = [new NamesCommand()],
        Donation = new DonationConfig
        {
            Url = "https://ko-fi.com/scobalula",
            Message = "Janitor is free and always will be. If it saved you some time, consider buying me a coffee!",
        },
    }, args);
}
