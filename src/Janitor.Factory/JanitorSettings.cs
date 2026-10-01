using Janitor.AssetHandling;
using RedFox.GameExtraction;

namespace Janitor.Factory;

/// <summary>
/// Provides the setting definitions, defaults, and export configuration mapping shared by the Janitor frontends.
/// </summary>
public static class JanitorSettings
{
    /// <summary>
    /// Gets the settings exposed by the Janitor frontends.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> Definitions { get; } =
    [
        new GameExtractionSetting
        {
            Name = "OutputDirectory",
            Group = "Export",
            Label = "Output directory",
            Type = GameExtractionSettingType.DirectoryPath,
            DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory(),
        },
        new GameExtractionSetting
        {
            Name = "Overwrite",
            Group = "Export",
            Label = "Overwrite existing files",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "PreserveDirectoryStructure",
            Group = "Export",
            Label = "Preserve directory structure",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ExportReferences",
            Group = "Export",
            Label = "Export referenced assets",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "ImageFormat",
            Group = "Images",
            Label = "Image format",
            Type = GameExtractionSettingType.Choice,
            Options = [".dds", ".png", ".tga"],
            DefaultValue = ".dds",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingImages",
            Group = "Images",
            Label = "Skip images that already exist",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "RelativeImages",
            Group = "Images",
            Label = "Export material images next to models",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "ModelFormats",
            Group = "Models",
            Label = "Model formats",
            Description = "Comma separated list of model file extensions.",
            Type = GameExtractionSettingType.Text,
            DefaultValue = ".semodel, .cast, .fbx",
        },
        new GameExtractionSetting
        {
            Name = "AnimationFormats",
            Group = "Animations",
            Label = "Animation formats",
            Description = "Comma separated list of animation file extensions.",
            Type = GameExtractionSettingType.Text,
            DefaultValue = ".seanim, .cast",
        },
        new GameExtractionSetting
        {
            Name = WwiseMediaResourceHandler.ConvertAudioOption,
            Group = "Audio",
            Label = "Convert audio to WAV",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
    ];

    /// <summary>
    /// Creates the default persisted setting values.
    /// </summary>
    /// <returns>The default settings.</returns>
    public static GameExtractionSettings CreateDefaults() => new()
    {
        Values = Definitions.ToDictionary(setting => setting.Name, setting => setting.DefaultValue?.ToString(), StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Builds an export configuration from persisted setting values.
    /// </summary>
    /// <param name="settings">The persisted settings.</param>
    /// <returns>The export configuration.</returns>
    public static ExportConfiguration CreateExportConfiguration(GameExtractionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string outputDirectory = GetValue(settings, "OutputDirectory");

        ExportConfiguration configuration = new()
        {
            OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? GameExtractionSettings.GetDefaultOutputDirectory() : outputDirectory,
            Overwrite = GetBoolean(settings, "Overwrite"),
            PreserveDirectoryStructure = GetBoolean(settings, "PreserveDirectoryStructure"),
            ExportReferences = GetBoolean(settings, "ExportReferences"),
        };

        configuration.SetOption("ImageFormats", new[] { GetValue(settings, "ImageFormat") });
        configuration.SetOption("SkipExistingImages", GetBoolean(settings, "SkipExistingImages"));
        configuration.SetOption("RelativeImages", GetBoolean(settings, "RelativeImages"));
        configuration.SetOption("ModelFormats", GetFormats(settings, "ModelFormats"));
        configuration.SetOption("AnimationFormats", GetFormats(settings, "AnimationFormats"));
        configuration.SetOption(WwiseMediaResourceHandler.ConvertAudioOption, GetBoolean(settings, WwiseMediaResourceHandler.ConvertAudioOption));

        return configuration;
    }

    private static string GetValue(GameExtractionSettings settings, string name) => settings.GetSettingValue(Definitions.First(setting => setting.Name == name)) ?? string.Empty;

    private static bool GetBoolean(GameExtractionSettings settings, string name) => bool.TryParse(GetValue(settings, name), out bool value) && value;

    private static string[] GetFormats(GameExtractionSettings settings, string name) => GetValue(settings, name).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
