using RedFox.GameExtraction;
using System.Globalization;

namespace Janitor.AssetHandling;

/// <summary>
/// Resolves the position multiplier applied to meshes, skeletons and animations, driven by the <c>ModelScale</c> setting.
/// </summary>
public static class ModelScale
{
    /// <summary>
    /// The multiplier applied when the setting is missing or unrecognised.
    /// </summary>
    public const float Default = 100.0f;

    /// <summary>
    /// Reads the configured multiplier from the active configuration.
    /// </summary>
    /// <param name="configuration">The active configuration.</param>
    /// <returns>The configured multiplier.</returns>
    public static float Read(GameExtractionConfiguration configuration) => Parse(configuration.GetOption("ModelScale", "100"));

    /// <summary>
    /// Parses the persisted multiplier, falling back to <see cref="Default"/> when it is missing, unrecognised or not finite.
    /// </summary>
    /// <param name="value">The persisted multiplier.</param>
    /// <returns>The parsed multiplier.</returns>
    public static float Parse(string? value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) && float.IsFinite(scale) ? scale : Default;
}
