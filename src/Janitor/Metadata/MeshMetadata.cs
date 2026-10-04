using RedFox.IO;
using System.Numerics;

namespace Janitor.Metadata;

/// <summary>
/// rend::MeshMetadata: describes a .binfbx mesh and where its buffers live within the file.
/// </summary>
public class MeshMetadata : IMetadata
{
    private const int ClutterLayerVersion = 38;

    /// <summary>
    /// Gets the minimum corner of the mesh bounding box.
    /// </summary>
    public Vector3 BoundBoxMin { get; private set; }

    /// <summary>
    /// Gets the maximum corner of the mesh bounding box.
    /// </summary>
    public Vector3 BoundBoxMax { get; private set; }

    /// <summary>
    /// Gets the centre of the mesh bounding sphere.
    /// </summary>
    public Vector3 BoundSphere { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the mesh is skinned.
    /// </summary>
    public bool HasBones { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the mesh has primitives without a material bound.
    /// </summary>
    public bool MissingMaterialBinds { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the mesh has more material binds than primitives.
    /// </summary>
    public bool HasExtraMaterialBinds { get; private set; }

    /// <summary>
    /// Gets the number of animation frames.
    /// </summary>
    public int NumAnimationFrames { get; private set; }

    /// <summary>
    /// Gets the LOD information flags.
    /// </summary>
    public int LodInformationFlags { get; private set; }

    /// <summary>
    /// Gets the LOD past which RBF deformation is disabled.
    /// </summary>
    public int RBFCutoffLod { get; private set; }

    /// <summary>
    /// Gets the LOD past which wrinkle maps are disabled.
    /// </summary>
    public int WrinkleCutoffLod { get; private set; }

    /// <summary>
    /// Gets the LOD past which dialogue idle animation is disabled; only present from version 38.
    /// </summary>
    public int DialogueIdleCutoffLod { get; private set; }

    /// <summary>
    /// Gets the number of LODs.
    /// </summary>
    public int NumberOfLods { get; private set; }

    /// <summary>
    /// Gets the byte count of generated LODs.
    /// </summary>
    public int GeneratedLodBytes { get; private set; }

    /// <summary>
    /// Gets the byte count of user created LODs.
    /// </summary>
    public int UserCreatedLodBytes { get; private set; }

    /// <summary>
    /// Gets the resource ID of the skeleton the mesh is bound to.
    /// </summary>
    public ulong SkeletonID { get; private set; }

    /// <summary>
    /// Gets the end offset of the CPU-side mesh header.
    /// </summary>
    public int CpuNumBytes { get; private set; }

    /// <summary>
    /// Gets the size of the MeshCPU block that precedes the mesh header.
    /// </summary>
    public int MeshCpuBytes { get; private set; }

    /// <summary>
    /// Gets the buffer regions of the individually streamed LODs.
    /// </summary>
    public List<MeshFileLayout> StreamableLODs { get; } = [];

    /// <summary>
    /// Gets the buffer region shared by the coarsest LODs.
    /// </summary>
    public MeshFileLayout TailLODs { get; private set; }

    /// <summary>
    /// Gets the number of LODs stored in <see cref="TailLODs"/>.
    /// </summary>
    public int NumTailLods { get; private set; }

    /// <summary>
    /// Gets the resource IDs of the sub meshes.
    /// </summary>
    public List<ulong> SubMeshIDs { get; } = [];

    /// <summary>
    /// Gets the resource ID of the geometry transform shader.
    /// </summary>
    public ulong GeometryTransformShaderID { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the mesh uses a geometry transform shader.
    /// </summary>
    public bool HasGeometryTransformShader => GeometryTransformShaderID != 0 && GeometryTransformShaderID != ulong.MaxValue;

    /// <summary>
    /// Gets the number of skinned variants.
    /// </summary>
    public int SkinnedVariantCount { get; private set; }

    /// <summary>
    /// Gets the clutter layer; only present from version 38.
    /// </summary>
    public int ClutterLayer { get; private set; }

    /// <summary>
    /// Gets the buffer region holding the given LOD.
    /// </summary>
    /// <param name="lodIndex">The LOD index.</param>
    /// <returns>The buffer region.</returns>
    public MeshFileLayout GetLayoutForLod(int lodIndex) => lodIndex < StreamableLODs.Count ? StreamableLODs[lodIndex] : TailLODs;

    /// <inheritdoc/>
    public void Parse(ReadOnlySpan<byte> buffer)
    {
        var reader = new SpanReader(buffer);
        var version = reader.Read<int>();

        _ = reader.Read<int>();

        BoundBoxMin = reader.Read<Vector3>();
        BoundBoxMax = reader.Read<Vector3>();

        _ = reader.Read<int>();

        BoundSphere = reader.Read<Vector3>();

        _ = reader.Read<float>();

        HasBones = reader.Read<byte>() == 1;
        MissingMaterialBinds = reader.Read<byte>() == 1;
        HasExtraMaterialBinds = reader.Read<byte>() == 1;

        // ResourceIDs are serialised as u32 (1) + u64.
        _ = reader.Read<int>();
        SkeletonID = reader.Read<ulong>();

        if (version < ClutterLayerVersion)
        {
            var unknownStringLength = reader.Read<int>();
            reader.Position += unknownStringLength;
        }

        NumAnimationFrames = reader.Read<int>();

        var lodTemplatePathLength = reader.Read<int>();
        reader.Position += lodTemplatePathLength;

        LodInformationFlags = reader.Read<int>();
        RBFCutoffLod = reader.Read<int>();
        WrinkleCutoffLod = reader.Read<int>();

        if (version >= ClutterLayerVersion)
            DialogueIdleCutoffLod = reader.Read<int>();

        NumberOfLods = reader.Read<int>();
        GeneratedLodBytes = reader.Read<int>();
        UserCreatedLodBytes = reader.Read<int>();

        // rend::MeshFileLayout
        _ = reader.Read<int>();
        CpuNumBytes = reader.Read<int>();
        MeshCpuBytes = reader.Read<int>();

        var streamableCount = reader.Read<int>();

        // Streamable regions, then the tail region.
        for (int i = 0; i <= streamableCount; i++)
        {
            var region = MeshFileLayout.Read(ref reader, i < streamableCount);

            if (region.Streamable)
                StreamableLODs.Add(region);
            else
                TailLODs = region;
        }

        NumTailLods = reader.Read<byte>();

        var subMeshCount = reader.Read<int>();

        for (int i = 0; i < subMeshCount; i++)
        {
            _ = reader.Read<int>();
            SubMeshIDs.Add(reader.Read<ulong>());
        }

        if (version >= ClutterLayerVersion)
            SkinnedVariantCount = reader.Read<int>();

        _ = reader.Read<int>();
        GeometryTransformShaderID = reader.Read<ulong>();

        if (version >= ClutterLayerVersion)
            ClutterLayer = reader.Read<int>();
        else
            SkinnedVariantCount = reader.Read<int>();
    }
}
