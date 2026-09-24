using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.Metadata;

public class MeshMetadata : IMetadata
{
    public Vector3 BoundBoxMin { get; private set; }
    public Vector3 BoundBoxMax { get; private set; }
    public Vector3 BoundSphere { get; private set; }

    public bool HasBones { get; private set; }
    public bool MissingMaterialBinds { get; private set; }
    public bool HasExtraMaterialBinds { get; private set; }
    public int NumAnimationFrames { get; private set; }
    public int LodInformationFlags { get; private set; }
    public int RBFCutoffLod { get; private set; }
    public int WrinkleCutoffLod { get; private set; }
    public int NumberOfLods { get; private set; }
    public int GeneratedLodBytes { get; private set; }
    public int UserCreatedLodBytes { get; private set; }

    public ulong SkeletonID { get; private set; }

    public int CpuNumBytes { get; private set; }

    public int MeshCpuBytes { get; private set; }

    public List<MeshFileLayout> StreamableLODs { get; } = [];

    public MeshFileLayout TailLODs { get; private set; }

    public int NumTailLods { get; private set; }

    public List<ulong> SubMeshIDs { get; } = [];

    public ulong GeometryTransformShaderID { get; private set; }

    public bool HasGeometryTransformShader => GeometryTransformShaderID != 0 && GeometryTransformShaderID != ulong.MaxValue;

    public int SkinnedVariantCount { get; private set; }

    public MeshFileLayout GetLayoutForLod(int lodIndex) => lodIndex < StreamableLODs.Count ? StreamableLODs[lodIndex] : TailLODs;

    public void Parse(ReadOnlySpan<byte> buffer)
    {
        // Pack File Meta Data
        var reader = new SpanReader(buffer);
        _ = reader.Read<uint>();
        _ = reader.Read<uint>();


        {
            BoundBoxMin = reader.Read<Vector3>();
            BoundBoxMax = reader.Read<Vector3>();
        }

        _ = reader.Read<int>();
        _ = reader.Read<int>();

        {
            BoundSphere = reader.Read<Vector3>();
        }

        HasBones = reader.Read<byte>() == 1;
        MissingMaterialBinds = reader.Read<byte>() == 1;
        HasExtraMaterialBinds = reader.Read<byte>() == 1;

        // v11 > 13
        _ = reader.Read<int>(); // version

        SkeletonID = reader.Read<ulong>();

        var length = reader.Read<int>();

        reader.Position += length;

        NumAnimationFrames = reader.Read<int>();

        var length2 = reader.Read<int>();

        reader.Position += length2;

        LodInformationFlags = reader.Read<int>();
        RBFCutoffLod = reader.Read<int>();
        WrinkleCutoffLod = reader.Read<int>();
        NumberOfLods = reader.Read<int>();
        GeneratedLodBytes = reader.Read<int>();
        UserCreatedLodBytes = reader.Read<int>();

        // rend::MeshFileLayout
        _ = reader.Read<int>(); // version (3)
        CpuNumBytes = reader.Read<int>();
        MeshCpuBytes = reader.Read<int>();

        var streamableCount = reader.Read<int>();

        // Streamable regions, then the tail region; each is a version (5), 12 values and a hash.
        for (int i = 0; i <= streamableCount; i++)
        {
            var region = new MeshFileLayout
            {
                MeshInfoVersion = reader.Read<int>(),
                FileOffset = reader.Read<int>(),
                VertexByteCount = reader.Read<int>(),
                PositionOnlyVertexByteCount = reader.Read<int>(),
                IndexByteCount = reader.Read<int>(),
                MeshletBytes = reader.Read<int>(),
                MeshletBoundsBytes = reader.Read<int>(),
                ClusterByteCount = reader.Read<int>(),
                ClusterCpuHeaderByteCount = reader.Read<int>(),
                OpacityMicromapIndexBytes = reader.Read<int>(),
                OpacityMicromapBytes = reader.Read<int>(),
                OpacityMicromapUsageBytes = reader.Read<int>(),
                IndexStride = reader.Read<int>(),
                Hash = reader.Read<int>(),
                Streamable = i < streamableCount
            };

            if (region.Streamable)
                StreamableLODs.Add(region);
            else
                TailLODs = region;
        }

        NumTailLods = reader.Read<byte>();

        // ResourceIDs are serialised as u32 (1) + u64.
        var subMeshCount = reader.Read<int>();

        for (int i = 0; i < subMeshCount; i++)
        {
            _ = reader.Read<int>();
            SubMeshIDs.Add(reader.Read<ulong>());
        }

        _ = reader.Read<int>();
        GeometryTransformShaderID = reader.Read<ulong>();
        SkinnedVariantCount = reader.Read<int>();
    }
}
