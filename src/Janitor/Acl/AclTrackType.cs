namespace Janitor.Acl;

/// <summary>
/// Defines the types of tracks an ACL compressed tracks buffer can contain.
/// </summary>
public enum AclTrackType : byte
{
    /// <summary>
    /// A single float per sample.
    /// </summary>
    Float1 = 0,

    /// <summary>
    /// Two floats per sample.
    /// </summary>
    Float2 = 1,

    /// <summary>
    /// Three floats per sample.
    /// </summary>
    Float3 = 2,

    /// <summary>
    /// Four floats per sample.
    /// </summary>
    Float4 = 3,

    /// <summary>
    /// A SIMD vector of four floats per sample.
    /// </summary>
    Vector4 = 4,

    /// <summary>
    /// A transform made of a rotation quaternion, a translation vector and a scale vector per sample.
    /// </summary>
    Qvv = 12,
}
