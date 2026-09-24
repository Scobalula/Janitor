using RedFox.IO;

namespace Janitor.Metadata;

/// <summary>
/// rend::MaterialMetadata: holds the resources a material references. From material version 0x14 (Control Resonant)
/// these are no longer stored in the .material file itself.
/// </summary>
public class MaterialMetadata : IMetadata
{
    /// <summary>
    /// Gets the resource IDs of the textures, indexed by the material's texture settings.
    /// </summary>
    public List<ulong> TextureResourceIDs { get; } = [];

    /// <summary>
    /// Gets the resource IDs of the sub materials.
    /// </summary>
    public List<ulong> MaterialResourceIDs { get; } = [];

    /// <summary>
    /// Gets the resource IDs of the extension shaders.
    /// </summary>
    public List<ulong> ExtensionResourceIDs { get; } = [];

    /// <inheritdoc/>
    public void Parse(ReadOnlySpan<byte> buffer)
    {
        var reader = new SpanReader(buffer);

        _ = reader.Read<int>();

        ReadResourceIDs(ref reader, TextureResourceIDs);
        ReadResourceIDs(ref reader, MaterialResourceIDs);
        ReadResourceIDs(ref reader, ExtensionResourceIDs);
    }

    private static void ReadResourceIDs(ref SpanReader reader, List<ulong> resourceIDs)
    {
        var count = reader.Read<int>();

        // ResourceIDs are serialised as u32 (1) + u64.
        for (int i = 0; i < count; i++)
        {
            _ = reader.Read<int>();
            resourceIDs.Add(reader.Read<ulong>());
        }
    }
}
