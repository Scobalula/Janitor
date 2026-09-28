namespace Janitor.Acl;

/// <summary>
/// Defines the formats ACL stores translation and scale sub-tracks in.
/// </summary>
public enum AclVectorFormat : byte
{
    /// <summary>
    /// Full precision vector, all three components are stored as floats.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Quantized vector, each component uses a variable number of bits.
    /// </summary>
    Variable = 1,
}
