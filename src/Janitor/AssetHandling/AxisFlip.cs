using System.Numerics;

namespace Janitor.AssetHandling;

/// <summary>
/// Mirrors model data across a coordinate axis, applying the same basis change to positions, directions and rotations so
/// meshes, skeletons and animations stay consistent with each other.
/// </summary>
public static class AxisFlip
{
    /// <summary>
    /// Parses the persisted axis name, falling back to <see cref="FlipAxis.None"/> when it is missing or unrecognised.
    /// </summary>
    /// <param name="value">The persisted axis name.</param>
    /// <returns>The parsed axis.</returns>
    public static FlipAxis Parse(string? value) => Enum.TryParse<FlipAxis>(value, true, out var axis) ? axis : FlipAxis.None;

    /// <summary>
    /// Mirrors a position or direction across the given axis, negating its component along that axis.
    /// </summary>
    /// <param name="value">The position or direction to mirror.</param>
    /// <param name="axis">The axis to mirror across.</param>
    /// <returns>The mirrored value.</returns>
    public static Vector3 Mirror(Vector3 value, FlipAxis axis) => axis switch
    {
        FlipAxis.X => new Vector3(-value.X, value.Y, value.Z),
        FlipAxis.Y => new Vector3(value.X, -value.Y, value.Z),
        FlipAxis.Z => new Vector3(value.X, value.Y, -value.Z),
        _ => value,
    };

    /// <summary>
    /// Mirrors a rotation across the given axis, the reflection conjugates the rotation so the components perpendicular to
    /// the axis negate.
    /// </summary>
    /// <param name="value">The rotation to mirror.</param>
    /// <param name="axis">The axis to mirror across.</param>
    /// <returns>The mirrored rotation.</returns>
    public static Quaternion Mirror(Quaternion value, FlipAxis axis) => axis switch
    {
        FlipAxis.X => new Quaternion(value.X, -value.Y, -value.Z, value.W),
        FlipAxis.Y => new Quaternion(-value.X, value.Y, -value.Z, value.W),
        FlipAxis.Z => new Quaternion(-value.X, -value.Y, value.Z, value.W),
        _ => value,
    };
}
