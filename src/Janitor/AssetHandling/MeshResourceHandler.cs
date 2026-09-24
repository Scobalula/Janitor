using Janitor.Metadata;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Groups;
using RedFox.IO;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public class MeshResourceHandler : ModelHandler
{
    private const uint LegacyVersion = 0x4E;

    private const uint MinimumVersion = 0x57;

    private const uint MaximumVersion = 0x5C;

    private static readonly byte[] AttributeSizes = [ 0x04, 0x08, 0x0C, 0x10, 0x04, 0x04, 0x04, 0x04, 0x08, 0x04, 0x08, 0x10, 0x08, 0x08, 0x01, 0x04, 0x02, ];

    /// <inheritdoc/>
    public override bool CanHandle(Asset asset)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File) // TODO: Rename this, ambigious
            return false;
        if (!asset.Name.EndsWith(".binfbx", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public override async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        // * Models are flipped, looks correct data-wise, even skeleton is flipped, game must be doing some transformation in vertex shader?
        // * Currently only supporting the first variant, files will have multiple variants.
        // * Currently only parsing highest LOD.
        // * Version 0x4E covers FBC: Firebreak and Alan Wake 2, 0x57 to 0x5C covers Control Resonant.
        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");
        if (!file.TryParseMetadata<MeshMetadata>("rend::MeshMetadata", out var meshMetadata))
            throw new InvalidDataException($"Missing rend::MeshMetadata on '{file.FullPath}'.");

        using var reader = new BinaryReader(file.Open());

        var resourceTable = context.GetRequiredService<ResourceTableService>().Resources;
        var scene = new Scene(asset.Name);

        // Files may start with a MeshCPU block (version 1, CPU-side triangle data); the mesh header follows it.
        reader.BaseStream.Position = meshMetadata.MeshCpuBytes;

        var version = reader.ReadUInt32();

        if (version != LegacyVersion && (version < MinimumVersion || version > MaximumVersion))
            throw new NotSupportedException($"Mesh version 0x{version:X}");

        SkeletonBone[]? skeletonBones = null;

        if (resourceTable.TryGetValue(meshMetadata.SkeletonID, out var skeletonResource))
        {
            if (skeletonResource.Data is not Asset skeletonAsset)
                throw new InvalidDataException($"Skeleton resource with ID 0x{meshMetadata.SkeletonID:X16} does not have an associated Asset.");

            var skeletonResult = await context.AssetManager.ReadAsync(skeletonAsset, cancellationToken);
            var skeletonRoot = skeletonResult.GetData<SceneNode>();

            scene.RootNode.AddNode(skeletonResult.GetData<SceneNode>());
            skeletonBones = skeletonRoot.GetAttribute<SkeletonBone[]>("OriginalTable");
        }

        var lodCount = reader.ReadInt32();
        var skeletonLodCount = reader.ReadInt32();
        var boneSetCount = reader.ReadInt32();

        var hasGeometryTransformShader = meshMetadata.HasGeometryTransformShader;

        if (version == LegacyVersion)
        {
            var boneTableCounts = reader.ReadStructArray<int>(boneSetCount).ToArray();

            foreach (var boneCount in boneTableCounts)
            {
                for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    var nameLength = reader.ReadInt32();
                    reader.BaseStream.Position += nameLength + 68;
                }
            }
        }
        else
        {
            var boneCount = 0;

            foreach (var count in reader.ReadStructArray<int>(boneSetCount))
                boneCount += count;

            reader.BaseStream.Position += 4L * boneSetCount;

            var nameBufferSize = reader.ReadInt32();

            if (boneSetCount > 0)
                reader.BaseStream.Position += nameBufferSize;

            reader.BaseStream.Position += 72L * boneCount;

            reader.ReadUInt64();
            var geometryTransformShaderId = reader.ReadUInt64();
            reader.ReadUInt64();

            hasGeometryTransformShader = geometryTransformShaderId is not (0 or 1 or ulong.MaxValue);
        }

        var boneMaps = new List<List<int>>(lodCount);

        if (skeletonLodCount > 0)
        {
            // Not really useful to us.
            for (int i = 0; i < skeletonLodCount; i++)
            {
                var count = reader.ReadInt32();
                var hash = reader.ReadInt32();

                reader.BaseStream.Position += 48 * count;
                reader.BaseStream.Position += 4 * count;
                reader.BaseStream.Position += 4 * count;
                reader.BaseStream.Position += 4 * count;
                reader.BaseStream.Position += 8 * ((long)(((ulong)count + 63) & 0xFFFFFFFFFFFFFFC0) >> 6);

                var count0 = reader.ReadInt64();

                reader.BaseStream.Position += 4 * count0;
            }


            for (int i = 0; i < lodCount; i++)
            {
                var unk = reader.ReadInt32();
                var boneMap = new List<int>(lodCount);

                for (int j = 0; j < unk; j++)
                {
                    boneMap.Add(reader.ReadInt32());
                }

                var unk2 = reader.ReadInt32();
                reader.BaseStream.Position += unk2 * 4;

                boneMaps.Add(boneMap);
            }

            var someSize = reader.ReadInt32();

            if (someSize > 0)
            {
                reader.BaseStream.Position += 4;
                reader.BaseStream.Position += someSize;
            }
        }

        reader.ReadSingle();

        var distanceCount = reader.ReadInt32();

        if (distanceCount < 0 || distanceCount > 64)
            throw new InvalidDataException($"Implausible LOD distance count {distanceCount}");

        var distances = reader.ReadStructArray<float>(distanceCount);

        var unknownSingle = reader.ReadSingle(); // floating point number

        var sphere = reader.ReadStruct<Vector4>();
        var boundsMin = reader.ReadStruct<Vector3>();
        var boundsMax = reader.ReadStruct<Vector3>();

        // Per-LOD bound spheres; also the quantisation frame for each LOD's 16-bit positions.
        var lodSpheres = reader.ReadStructArray<Vector4>(lodCount).ToArray();

        // Skinning bounds (sphere + box), present when the mesh uses a geometry transform shader.
        if (skeletonLodCount > 0 && hasGeometryTransformShader)
            reader.BaseStream.Position += 40;

        var materialCount = reader.ReadInt32();
        var uniqueMaterials = new Dictionary<ulong, Material>();
        var allMaterials = new Material[materialCount];

        for (var materialIndex = 0; materialIndex < materialCount; materialIndex++)
        {
            var materialResourceId = reader.ReadUInt64();

            if (!uniqueMaterials.TryGetValue(materialResourceId, out var material))
            {
                if (!resourceTable.TryGetValue(materialResourceId, out var materialResourceFile))
                    throw new KeyNotFoundException($"Material resource with ID 0x{materialResourceId:X16} not found in resource table.");
                if (materialResourceFile.Data is not Asset materialAsset)
                    throw new InvalidDataException($"Material resource with ID 0x{materialResourceId:X16} does not have an associated Asset.");

                var materialResult = await context.AssetManager.ReadAsync(materialAsset, cancellationToken);

                material = materialResult.GetData<Material>();

                if (scene.RootNode.TryFindChild(material.Name, out _))
                    material.Name = $"{material.Name}_{materialResourceId:X16}";

                uniqueMaterials[materialResourceId] = material;
                scene.RootNode.AddNode(material);
            }

            allMaterials[materialIndex] = material;
        }

        var materialIndexCount = reader.ReadInt32();
        List<MeshVariant> variants = [new MeshVariant()
        {
            Name = "Default",
            MaterialIndices = reader.ReadStructArray<int>(materialIndexCount).ToArray()
        }];

        var variantCount = reader.ReadInt32();

        for (int i = 0; i < variantCount; i++)
        {
            variants.Add(new MeshVariant()
            {
                Name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())).TrimEnd('\0'),
                MaterialIndices = reader.ReadStructArray<int>(materialIndexCount).ToArray()
            });
        }

        if (version >= 0x58)
            reader.ReadStructArray<int>(reader.ReadInt32());
        if (version >= 0x54)
            reader.BaseStream.Position += 1;
        if (version >= 0x59)
            reader.BaseStream.Position += 1;
        if (version >= 0x5B)
            reader.ReadBytes(reader.ReadInt32() * (version >= 0x5C ? 20 : 16));

        var primitiveCount = reader.ReadInt32();
        var primitives = new List<MeshPrimitive>(primitiveCount);

        for (int i = 0; i < primitiveCount; i++)
        {
            var partOffset = reader.BaseStream.Position;

            var lodID = reader.ReadInt32();
            var vertexCount = reader.ReadInt32();
            var faceCount = reader.ReadInt32();

            reader.ReadInt32(); // Always 0 in shipped files
            reader.ReadInt32(); // Always 0 in shipped files

            var faceIndexSize = reader.ReadInt32();
            var faceOffset = reader.ReadInt32();
            var influenceCount = reader.ReadInt32();

            var primitiveSphere = reader.ReadStruct<Vector4>();
            var primitiveMin = reader.ReadStruct<Vector3>();
            var primitiveMax = reader.ReadStruct<Vector3>();

            reader.ReadInt32(); // Unused?

            var attributeCount = reader.ReadByte();
            var attributes = new MeshAttribute[attributeCount];
            var vertexPositionSize = 0;
            var vertexSize = 0;

            for (int m = 0; m < attributeCount; m++)
            {
                var positionAttribute = reader.ReadByte() == 0;
                var dataType = reader.ReadByte();
                var usage = reader.ReadByte();
                var padding = reader.ReadByte();
                var sizeOfAttribute = AttributeSizes[dataType];

                attributes[m] = new()
                {
                    PositionAttribute = positionAttribute,
                    DataType = dataType,
                    Usage = usage,
                    Offset = positionAttribute ? vertexPositionSize : vertexSize,
                    Size = sizeOfAttribute,
                };

                if (positionAttribute)
                {
                    vertexPositionSize += sizeOfAttribute;
                }
                else
                {
                    vertexSize += sizeOfAttribute;
                }
            }

            // Random stuff at end - preserving the type read
            // and how the exe consumes them in-case they are
            // actually needed, but seem to be game-specific.
            reader.ReadInt32();
            reader.ReadSingle();

            reader.ReadByte();

            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();

            if (version == LegacyVersion)
            {
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
            }
            else
            {
                if (version >= 0x5A)
                    reader.ReadInt32();

                reader.ReadInt32();
            }

            reader.ReadByte();

            var opacityMicromapCount = reader.ReadInt32();

            reader.BaseStream.Position += (version == LegacyVersion ? 40L : 36L) * opacityMicromapCount;

            primitives.Add(new MeshPrimitive()
            {
                VertexPositionSize = vertexPositionSize,
                VertexSize = vertexSize,
                LODIndex = lodID,
                VertexCount = vertexCount,
                FaceCount = faceCount,
                PositionOnlyVertexOffset = 0, // assigned per LOD in BuildLods
                VertexOffset = 0,
                FaceOffset = faceOffset,
                PartOffset = (int)partOffset,
                FaceIndexSize = faceIndexSize,
                MaterialIndex = i,
                Min = primitiveMin,
                Max = primitiveMax,
                Center = new(lodSpheres[lodID].X, lodSpheres[lodID].Y, lodSpheres[lodID].Z),
                Scale = lodSpheres[lodID].W,
                Attributes = attributes,
            });
        }

        reader.ReadUInt32(); // Trailer

        if (reader.BaseStream.Position != meshMetadata.CpuNumBytes)
            throw new InvalidDataException($"Mesh header of '{file.FullPath}' ended at 0x{reader.BaseStream.Position:X}, metadata says 0x{meshMetadata.CpuNumBytes:X}.");

        var lods = BuildLods(meshMetadata, lodCount, primitives);

        for (int l = 0; l < lodCount; l++)
        {
            var lodToExport = lods[l];

            // Variants only swap materials (the geometry is identical), so each variant becomes its own group,
            // and is exported as its own model, with meshes that share the default variant's buffers.
            var variantGroups = new MeshGroup[variants.Count];

            for (int v = 0; v < variants.Count; v++)
            {
                var groupName = v == 0 ? $"LOD_{l}" : $"LOD_{l}_{SanitizeName(variants[v].Name)}";
                variantGroups[v] = scene.RootNode.AddNode(new MeshGroup(groupName, SceneNodeFlags.None));
            }

            reader.BaseStream.Position = lodToExport.Layout.FileOffset;

            var vertexBytes = reader.ReadBytes(lodToExport.Layout.VertexByteCount);
            var positionOnlyVertexBytes = reader.ReadBytes(lodToExport.Layout.PositionOnlyVertexByteCount);
            var indexBytes = reader.ReadBytes(lodToExport.Layout.IndexByteCount);

            var faceReader = new SpanReader(indexBytes);

            foreach (var primitive in lodToExport.Primitives)
            {
                var vertexMap = new int[primitive.VertexCount];

                // Bind vertex attributes to their stream: stream 0 lives in the position-only blob,
                // every other stream in the vertex blob. The game can store attributes in any order,
                // and may split skin weights across several attributes.
                MeshAttributeBuffer BindAttribute(MeshAttribute? attribute) => new()
                {
                    Buffer = attribute?.PositionAttribute == true ? positionOnlyVertexBytes : vertexBytes,
                    ByteOffset = attribute?.PositionAttribute == true ? primitive.PositionOnlyVertexOffset : primitive.VertexOffset,
                    Attribute = attribute,
                    Stride = attribute?.PositionAttribute == true ? primitive.VertexPositionSize : primitive.VertexSize
                };

                MeshAttribute? FindAttribute(int usage) => primitive.Attributes.Any(x => x.Usage == usage) ? primitive.Attributes.First(x => x.Usage == usage) : null;

                var positionsAttribute = FindAttribute(0) ?? throw new InvalidDataException($"Primitive in LOD {l} has no POSITION attribute.");

                var buffer = new MeshPrimitiveBuffer
                {
                    Primitive = primitive,
                    PositionBuffer = BindAttribute(positionsAttribute),
                    NormalBuffer = BindAttribute(FindAttribute(1)),
                    UVBuffer = BindAttribute(FindAttribute(2)),
                    BoneMap = boneMaps.Count > 0 ? boneMaps[primitive.LODIndex] : []
                };

                foreach (var indicesAttribute in primitive.Attributes.Where(x => x.Usage == 5))
                    buffer.BlendIndicesBuffers.Add(BindAttribute(indicesAttribute));
                foreach (var weightsAttribute in primitive.Attributes.Where(x => x.Usage == 6))
                    buffer.BlendWeightsBuffers.Add(BindAttribute(weightsAttribute));

                var mesh = new Mesh
                {
                    Positions   = new DataBuffer<float>(primitive.VertexCount, 1, 3),
                    Normals     = new DataBuffer<float>(primitive.VertexCount, 1, 3),
                    UVLayers    = new DataBuffer<float>(primitive.VertexCount, 1, 2),
                    FaceIndices = new DataBuffer<int>(primitive.FaceCount, 1, 1),
                    Materials = [allMaterials[variants[0].MaterialIndices[primitive.MaterialIndex]]]
                };

                if (buffer.BlendIndicesBuffers.Count > 0)
                {
                    mesh.BoneWeights = new DataBuffer<float>(primitive.VertexCount, buffer.BlendIndicesBuffers.Count * 4, 1);
                    mesh.BoneIndices = new DataBuffer<int>(primitive.VertexCount, buffer.BlendIndicesBuffers.Count * 4, 1);
                }

                faceReader.Position = primitive.FaceOffset * primitive.FaceIndexSize;

                // We'll build a map, as the game usually passes 1 big buffer to the GPU for all submeshes,
                // the problem being that these indices are global, we want local for intermediate formats
                // TODO: Do a pass where we precache all vertices?
                for (int i = 0; i < primitive.FaceCount; i++)
                {
                    var i0 = primitive.FaceIndexSize == 4 ? faceReader.Read<int>() : faceReader.Read<ushort>();
                    var i1 = primitive.FaceIndexSize == 4 ? faceReader.Read<int>() : faceReader.Read<ushort>();
                    var i2 = primitive.FaceIndexSize == 4 ? faceReader.Read<int>() : faceReader.Read<ushort>();

                    var ri0 = vertexMap[i0];
                    var ri1 = vertexMap[i1];
                    var ri2 = vertexMap[i2];

                    if (ri0 == 0)
                    {
                        ri0 = buffer.CreateVertexOnOutputMesh(i0, mesh);
                        vertexMap[i0] = ri0;
                    }
                    if (ri1 == 0)
                    {
                        ri1 = buffer.CreateVertexOnOutputMesh(i1, mesh);
                        vertexMap[i1] = ri1;
                    }
                    if (ri2 == 0)
                    {
                        ri2 = buffer.CreateVertexOnOutputMesh(i2, mesh);
                        vertexMap[i2] = ri2;
                    }

                    ri0--;
                    ri1--;
                    ri2--;

                    mesh.FaceIndices?.Add(ri0);
                    mesh.FaceIndices?.Add(ri1);
                    mesh.FaceIndices?.Add(ri2);
                }

                mesh.SetSkinBinding(skeletonBones);

                variantGroups[0].AddNode(mesh);

                for (int v = 1; v < variants.Count; v++)
                {
                    var variantMesh = new Mesh
                    {
                        Positions   = mesh.Positions,
                        Normals     = mesh.Normals,
                        UVLayers    = mesh.UVLayers,
                        FaceIndices = mesh.FaceIndices,
                        BoneIndices = mesh.BoneIndices,
                        BoneWeights = mesh.BoneWeights,
                        Materials = [allMaterials[variants[v].MaterialIndices[primitive.MaterialIndex]]]
                    };

                    variantMesh.SetSkinBinding(skeletonBones);
                    variantGroups[v].AddNode(variantMesh);
                }
            }

            break;
        }

        return new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = scene
        };
    }

    /// <summary>Makes a variant name safe to use in an exported file name.</summary>
    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    /// <summary>
    /// Maps every LOD to its buffer region and works out where its vertices start. Streamable LODs have a
    /// region each; the coarsest LODs share the tail region, where each LOD's vertices follow the previous
    /// (coarser) LOD's while its indices restart at 0.
    /// </summary>
    private static MeshLod[] BuildLods(MeshMetadata meshMetadata, int lodCount, List<MeshPrimitive> primitives)
    {
        var lods = Enumerable.Range(0, lodCount).Select(i => new MeshLod(i, meshMetadata.GetLayoutForLod(i))).ToArray();

        foreach (var primitive in primitives)
            lods[primitive.LODIndex].Primitives.Add(primitive);

        foreach (var region in lods.GroupBy(lod => lod.Layout.FileOffset))
        {
            var vertexOffset = 0;
            var positionOnlyVertexOffset = 0;

            foreach (var lod in region.OrderByDescending(lod => lod.Index))
            {
                lod.VertexOffset = vertexOffset;
                lod.PositionOnlyVertexOffset = positionOnlyVertexOffset;

                for (int i = 0; i < lod.Primitives.Count; i++)
                {
                    lod.Primitives[i] = lod.Primitives[i] with
                    {
                        VertexOffset = vertexOffset,
                        PositionOnlyVertexOffset = positionOnlyVertexOffset
                    };
                }

                if (lod.Primitives.Count > 0)
                {
                    vertexOffset += lod.Primitives[0].VertexCount * lod.Primitives[0].VertexSize;
                    positionOnlyVertexOffset += lod.Primitives[0].VertexCount * lod.Primitives[0].VertexPositionSize;
                }
            }
        }

        return lods;
    }
}
