namespace Janitor.AssetHandling;

/// <summary>
/// Identifies the axis model data is mirrored across, driven by the <c>FlipModelsAxis</c> setting.
/// </summary>
public enum FlipAxis
{
    /// <summary>
    /// No mirroring is applied.
    /// </summary>
    None,

    /// <summary>
    /// Mirror across the X axis, negating the X component.
    /// </summary>
    X,

    /// <summary>
    /// Mirror across the Y axis, negating the Y component.
    /// </summary>
    Y,

    /// <summary>
    /// Mirror across the Z axis, negating the Z component.
    /// </summary>
    Z,
}
