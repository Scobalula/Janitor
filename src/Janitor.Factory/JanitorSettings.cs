using Janitor.AssetHandling;
using RedFox.GameExtraction;

namespace Janitor.Factory;

/// <summary>
/// Provides the setting definitions and defaults shared by the Janitor frontends.
/// </summary>
public static class JanitorSettings
{
    /// <summary>
    /// Gets the settings exposed by the Janitor frontends.
    /// </summary>
    public static IReadOnlyList<GameExtractionSetting> Definitions { get; } =
    [
        // General Settings
        new GameExtractionSetting
        {
            Name = "OutputDirectory",
            Group = GameExtractionSettingGroup.Export,
            Label = "Output directory",
            Type = GameExtractionSettingType.DirectoryPath,
            DefaultValue = GameExtractionSettings.GetDefaultOutputDirectory("Janitor"),
        },
        new GameExtractionSetting
        {
            Name = "ReadRawAssets",
            Group = GameExtractionSettingGroup.Export,
            Label = "Read and export raw assets.",
            Type = GameExtractionSettingType.Boolean,
            Description = "Read assets as-is from the package. This setting also affects the previewer.",
            DefaultValue = false,
        },
        GameExtractionLogging.VerboseSetting,
        new GameExtractionSetting
        {
            Name = "ImageFormats",
            Group = GameExtractionSettingGroup.Image,
            Label = "Image formats",
            Description = "Comma-separated list of image extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".dds",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingImages",
            Group = GameExtractionSettingGroup.Image,
            Label = "Skip images that already exist",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "RelativeImages",
            Group = GameExtractionSettingGroup.Image,
            Label = "Export material images to a flat folder",
            Description = "Store each material's textures together instead of preserving their paths.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        // Model Settings
        new GameExtractionSetting
        {
            Name = "ModelFormats",
            Group = GameExtractionSettingGroup.Model,
            Label = "Model formats",
            Description = "List of export extensions/formats.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".cast, .semodel",
        },
        new GameExtractionSetting
        {
            Name = "SkeletonFormats",
            Group = GameExtractionSettingGroup.Model,
            Label = "Skeleton formats",
            Description = "List of export extensions/formats.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".cast, .semodel",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingSkeletons",
            Group = GameExtractionSettingGroup.Model,
            Label = "Skip skeletons that already exist",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ExportModelImages",
            Group = GameExtractionSettingGroup.Model,
            Label = "Export images with models.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "RelativeModelImages",
            Group = GameExtractionSettingGroup.Model,
            Label = "Export model images to the model's folder.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = false,
        },
        new GameExtractionSetting
        {
            Name = "ReadAllLODs",
            Group = GameExtractionSettingGroup.Model,
            Label = "Read and export all model LODs.",
            Description = "This setting also affects the previewer.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "ReadAllVariants",
            Group = GameExtractionSettingGroup.Model,
            Label = "Read and export all model variants.",
            Description = "This setting also affects the previewer.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingModels",
            Group = GameExtractionSettingGroup.Model,
            Label = "Skip already exported models.",
            Type = GameExtractionSettingType.Boolean,
            DefaultValue = true,
        },
        new GameExtractionSetting
        {
            Name = "AnimationFormats",
            Group = GameExtractionSettingGroup.Animation,
            Label = "Animation formats",
            Description = "Comma separated list of animation file extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".seanim, .cast",
        },
        new GameExtractionSetting
        {
            Name = "AudioFormats",
            Group = GameExtractionSettingGroup.Sound,
            Label = "Audio formats",
            Description = "Comma-separated list of audio extensions.",
            Type = GameExtractionSettingType.TextArray,
            DefaultValue = ".wav",
        },
        new GameExtractionSetting
        {
            Name = "SkipExistingAudio",
            Group = GameExtractionSettingGroup.Sound,
            Label = "Skip audio that already exists",
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

}
