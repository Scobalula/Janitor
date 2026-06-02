using Janitor.AssetHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Janitor.Metadata
{
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
        public required int OpacityMicromapIndexBytes { get; set; }
        public required int OpacityMicromapBytes { get; set; }
        public required int OpacityMicromapUsageBytes { get; set; }
        public required int IndexStride { get; set; }
        public required int Crc { get; set; }

        public required bool Streamable { get; set; }
    }
}
