namespace Janitor.Acl;

/// <summary>
/// Defines the formats ACL stores rotation sub-tracks in.
/// </summary>
public enum AclRotationFormat : byte
{
    /// <summary>
    /// Full precision quaternion, all four components are stored as floats.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Full precision quaternion with the W component dropped and reconstructed at decompression.
    /// </summary>
    DropWFull = 2,

    /// <summary>
    /// Quantized quaternion with the W component dropped, each component uses a variable number of bits.
    /// </summary>
    DropWVariable = 3,
}
