using Janitor.Factory;
using RedFox.GameExtraction;
using RedFox.GameExtraction.UI;
using RedFox.GameExtraction.UI.ViewModels;

namespace Janitor.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
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
            Version = "1.0.0",
            AccentColor = "#037599",
            FileFilter = "Pack2 TOC Files|*.rmdtoc|All Files|*.*",
            SupportsFileSources = true,
            SupportsDirectorySources = false,
            SupportsProcessSources = false,
            Settings = JanitorSettings.CreateDefaults(),
            SettingDefinitions = [.. JanitorSettings.Definitions, .. ScenePreviewSettings.SettingDefinitions],
            About = new AboutConfig
            {
                Description = "An asset extractor for Northlight Engine games, including Alan Wake 2, Control, and FBC: Firebreak.",
            },
        };

        GameExtractionApp.Run(config);
    }
}
