using System.Numerics;

namespace Janitor.Acl;

/// <summary>
/// Holds the decompressed samples of a single ACL transform track. Each sub-track holds one sample per clip sample when
/// animated, a single sample when constant or trivially defaulted, and no samples when its default value comes from the bind pose.
/// </summary>
public sealed class AclTransformTrack
{
    /// <summary>
    /// Gets or sets the local rotation samples.
    /// </summary>
    public Quaternion[] Rotations { get; set; } = [];

    /// <summary>
    /// Gets or sets the local translation samples.
    /// </summary>
    public Vector3[] Translations { get; set; } = [];

    /// <summary>
    /// Gets or sets the local scale samples.
    /// </summary>
    public Vector3[] Scales { get; set; } = [];
}
