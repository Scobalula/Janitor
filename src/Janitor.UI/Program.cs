using Janitor.Factory;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.ViewModels;

namespace Janitor.UI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        GameExtractionConfig config = new()
        {
            AssetManagerFactory = JanitorAssetManagerFactory.Create,
            WindowTitle = "Janitor - Northlight Asset Extractor",
            SidebarTitle = "Janitor",
            Description = "Northlight Extraction Tool",
            AppName = "Janitor",
            IconPath = Path.Combine(AppContext.BaseDirectory, "Icon.png"),
            SidebarIconPath = Path.Combine(AppContext.BaseDirectory, "Icon.png"),
            Version = JanitorBuildInfo.DisplayVersion,
            AccentColor = "#037599",
            FileFilter = "Pack2 TOC Files|*.rmdtoc|All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = false,
            Settings = JanitorSettings.CreateDefaults(),
            SettingDefinitions = [.. JanitorSettings.Definitions, .. PreviewSettings.SettingDefinitions],
            About = new AboutConfig
            {
                Description = "An asset extractor for Northlight Engine games, including Alan Wake 2, Control, and FBC: Firebreak.",
                Links = [ 
                    new(){ Label = "Github", Url = "https://github.com/Scobalula/Janitor"},
                    new(){ Label = "X", Url = "https://x.com/scobalula"},
                    new(){ Label = "Donate", Url = "https://ko-fi.com/scobalula"}],
            },
            Donation = new DonationConfig
            {
                Url = "https://ko-fi.com/scobalula",
                Message = "Janitor is free and always will be. If it saved you some time, consider buying me a coffee!",
            },
        };

        GameExtractionApp.Run(config, args);
    }
}
