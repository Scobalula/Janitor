namespace Janitor.Metadata
{
    /// <summary>
    /// rend::MeshFileLayout::StreamableRegion: one buffer region in a .binfbx. The ten byte counts are
    /// stored back to back in this order starting at <see cref="FileOffset"/> (rend::MeshBufferSet::load).
    /// </summary>
    public struct MeshFileLayout
    {
        public required int MeshInfoVersion { get; set; }
        public required int FileOffset { get; set; }
        public required int VertexByteCount { get; set; }
        public required int PositionOnlyVertexByteCount { get; set; }
        public required int IndexByteCount { get; set; }
        public required int MeshletBytes { get; set; }
        public required int MeshletBoundsBytes { get; set; }
        public required int ClusterByteCount { get; set; }
        public required int ClusterCpuHeaderByteCount { get; set; }
        public required int OpacityMicromapIndexBytes { get; set; }
        public required int OpacityMicromapBytes { get; set; }
        public required int OpacityMicromapUsageBytes { get; set; }
        public required int IndexStride { get; set; }
        public required int Hash { get; set; }

        public required bool Streamable { get; set; }
    }
}
