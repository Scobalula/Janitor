using System.Numerics;

namespace Janitor.Animation;

/// <summary>
/// Describes a clip that an animation graph samples as an additive layer, along with the base pose its samples are made relative to.
/// </summary>
/// <param name="clipResourceId">The resource ID of the clip.</param>
/// <param name="inverseBasePose">The inverse base pose, keyed by the FNV-1a hash of each bone's lower case name.</param>
public sealed class AdditiveClip(ulong clipResourceId, IReadOnlyDictionary<uint, (Quaternion Rotation, Vector3 Translation)> inverseBasePose)
{
    /// <summary>
    /// Gets the resource ID of the clip.
    /// </summary>
    public ulong ClipResourceId { get; } = clipResourceId;

    /// <summary>
    /// Gets the inverse base pose, in meters, keyed by the FNV-1a hash of each bone's lower case name.
    /// </summary>
    public IReadOnlyDictionary<uint, (Quaternion Rotation, Vector3 Translation)> InverseBasePose { get; } = inverseBasePose;
}
