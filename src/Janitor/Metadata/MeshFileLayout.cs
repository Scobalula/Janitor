using RedFox.IO;

namespace Janitor.Metadata;

/// <summary>
/// rend::MeshFileLayout::StreamableRegion: one buffer region in a .binfbx. The byte counts are
/// stored back to back in this order starting at <see cref="FileOffset"/> (rend::MeshBufferSet::load).
/// </summary>
public struct MeshFileLayout
{
    /// <summary>
    /// Gets or sets the region version; version 5 (Alan Wake 2) adds the cluster byte counts, version 3 (Control Resonant) omits them.
    /// </summary>
    public required int MeshInfoVersion { get; set; }

    /// <summary>
    /// Gets or sets the offset of the region within the file.
    /// </summary>
    public required int FileOffset { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the vertex buffer.
    /// </summary>
    public required int VertexByteCount { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the position-only vertex buffer.
    /// </summary>
    public required int PositionOnlyVertexByteCount { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the index buffer.
    /// </summary>
    public required int IndexByteCount { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the meshlet buffer.
    /// </summary>
    public required int MeshletBytes { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the meshlet bounds buffer.
    /// </summary>
    public required int MeshletBoundsBytes { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the cluster buffer; zero before version 5.
    /// </summary>
    public required int ClusterByteCount { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the cluster CPU header buffer; zero before version 5.
    /// </summary>
    public required int ClusterCpuHeaderByteCount { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the opacity micromap index buffer.
    /// </summary>
    public required int OpacityMicromapIndexBytes { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the opacity micromap buffer.
    /// </summary>
    public required int OpacityMicromapBytes { get; set; }

    /// <summary>
    /// Gets or sets the byte count of the opacity micromap usage buffer.
    /// </summary>
    public required int OpacityMicromapUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the size of a single index in bytes.
    /// </summary>
    public required int IndexStride { get; set; }

    /// <summary>
    /// Gets or sets the region hash.
    /// </summary>
    public required int Hash { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this region is streamed individually rather than holding the tail LODs.
    /// </summary>
    public required bool Streamable { get; set; }

    /// <summary>
    /// Reads a region from the provided reader.
    /// </summary>
    /// <param name="reader">The reader positioned at the region.</param>
    /// <param name="streamable">Whether the region is a streamable region.</param>
    /// <returns>The region.</returns>
    public static MeshFileLayout Read(ref SpanReader reader, bool streamable)
    {
        var version = reader.Read<int>();

        return new()
        {
            MeshInfoVersion = version,
            FileOffset = reader.Read<int>(),
            VertexByteCount = reader.Read<int>(),
            PositionOnlyVertexByteCount = reader.Read<int>(),
            IndexByteCount = reader.Read<int>(),
            MeshletBytes = reader.Read<int>(),
            MeshletBoundsBytes = reader.Read<int>(),
            ClusterByteCount = version >= 5 ? reader.Read<int>() : 0,
            ClusterCpuHeaderByteCount = version >= 5 ? reader.Read<int>() : 0,
            OpacityMicromapIndexBytes = reader.Read<int>(),
            OpacityMicromapBytes = reader.Read<int>(),
            OpacityMicromapUsageBytes = reader.Read<int>(),
            IndexStride = reader.Read<int>(),
            Hash = reader.Read<int>(),
            Streamable = streamable
        };
    }
}
