using Avalonia.Controls;
using Avalonia.Platform;
using Cast.NET.Nodes;
using Janitor.Metadata;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Buffers;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public class MeshResourceHandler : IAssetHandler
{
    private static readonly byte[] AttributeSizes = [ 0x04, 0x08, 0x0C, 0x10, 0x04, 0x04, 0x04, 0x04, 0x08, 0x04, 0x08, 0x10, 0x08, 0x08, 0x01, 0x04, 0x02, ];

    /// <inheritdoc/>
    public bool CanHandle(Asset asset)
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
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var imageManager = context.GetRequiredService<ImageTranslatorService>().Manager;
        var manager = context.AssetManager.GetRequiredService<SceneTranslatorService>().Manager;
        var scene = result.GetData<Scene>();

        var imageFormats = context.ExportConfiguration.GetOption("ImageFormats", MaterialResourceHandler.DefaultValue);

        TextureResourceHandler.ExportMaterialImages(scene.EnumerateDescendants<Texture>(), imageFormats, imageManager, context.ExportConfiguration.OutputDirectory, true, true);

        foreach (var group in scene.EnumerateChildren<Group>("LOD*"))
        {
            group.Flags |= SceneNodeFlags.Selected;
            manager.Write("test.semodel", scene, new(), cancellationToken);
            group.Flags ^= SceneNodeFlags.Selected;

            break;
        }

        return;
    }

    /// <inheritdoc/>
    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");
        if (!file.TryParseMetadata<MeshMetadata>("rend::MeshMetadata", out var meshMetadata))
            throw new InvalidDataException($"Missing rend::MeshMetadata on '{file.FullPath}'.");

        using var reader = new BinaryReader(file.Open());

        var resourceTable = context.GetRequiredService<ResourceTableService>().Resources;
        var scene = new Scene(asset.Name);

        var version = reader.ReadUInt32();
        if (version != 0x4D)
            throw new InvalidDataException($"Invalid Mesh Resource version: 0x{version:X8}.");

        if (resourceTable.TryGetValue(meshMetadata.SkeletonID, out var skeletonResource))
        {
            if (skeletonResource.Data is not Asset skeletonAsset)
                throw new InvalidDataException($"Skeleton resource with ID 0x{meshMetadata.SkeletonID:X16} does not have an associated Asset.");

            var skeletonResult = await context.AssetManager.ReadAsync(skeletonAsset, cancellationToken);
            scene.RootNode.AddNode(skeletonResult.GetData<SceneNode>());
        }

        var lodCount = reader.ReadInt32();
        var skeletonCount = reader.ReadInt32();
        var boneTableCount = reader.ReadInt32();

        var boneTableCounts = reader.ReadStructArray<int>(boneTableCount);

        for (var tableIndex = 0; tableIndex < boneTableCounts.Length; tableIndex++)
        {
            for (var entryIndex = 0; entryIndex < boneTableCounts[tableIndex]; entryIndex++)
            {
                var nameLength = reader.ReadInt32();
                reader.BaseStream.Position += nameLength;
                reader.BaseStream.Position += 68;
            }
        }

        var boneMaps = new List<List<int>>(lodCount);

        var someSize = 0;

        if (skeletonCount > 0)
        {
            // Not really useful to us.
            for (int i = 0; i < skeletonCount; i++)
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

            someSize = reader.ReadInt32();

            if (someSize > 0)
            {
                reader.BaseStream.Position += 4;
                reader.BaseStream.Position += someSize;
            }
        }

        reader.ReadSingle();

        var uink = reader.ReadInt32();

        for (int i = 0; i < uink; i++)
        {
            reader.BaseStream.Position += 4;
        }

        var a1 = reader.ReadSingle(); // floating point number 

        var cx = reader.ReadSingle(); // floating point number 
        var cy = reader.ReadSingle(); // floating point number 
        var cz = reader.ReadSingle(); // floating point number 
        var cr = reader.ReadSingle(); // floating point number 

        var mix = reader.ReadSingle(); // floating point number 
        var miy = reader.ReadSingle(); // floating point number 
        var miz = reader.ReadSingle(); // floating point number 
        var max = reader.ReadSingle(); // floating point number 
        var may = reader.ReadSingle(); // floating point number 
        var maz = reader.ReadSingle(); // floating point number 

        for (int i = 0; i < lodCount; i++)
        {
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
        }

        if (skeletonCount > 0 && someSize > 0)
        {
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
            reader.ReadSingle(); // floating point number
        }


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
                Name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())),
                MaterialIndices = reader.ReadStructArray<int>(materialIndexCount).ToArray()
            });
        }

        var primitiveCount = reader.ReadInt32();
        var lods = meshMetadata.StreamableLODs.Select(lod => new MeshLod(lod)).ToArray();

        for (int i = 0; i < primitiveCount; i++)
        {
            var partOffset = reader.BaseStream.Position;
            var lodID = reader.ReadInt32();
            var vertexCount = reader.ReadInt32();
            var faceCount = reader.ReadInt32();

            reader.ReadInt32();
            reader.ReadInt32();

            var faceIndexSize = reader.ReadInt32();
            var faceOffset = reader.ReadInt32();
            var influenceCount = reader.ReadInt32();

            var boundsC = reader.ReadStruct<Vector4>();
            var minVec = reader.ReadStruct<Vector3>();
            var maxVec = reader.ReadStruct<Vector3>();

            reader.ReadInt32(); // Unused?

            var attributeCount = reader.ReadByte();
            var vertexBytesOffset = 0;
            var positionOnlyVertexBytesOffset = 0;
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

                attributes[m] =new()
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

            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();

            reader.ReadByte();

            var randomInts = reader.ReadInt32();

            for (int l = 0; l < randomInts; l++)
            {
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
            }

            lods[lodID].Primitives.Add(new MeshPrimitive()
            {
                VertexPositionSize = vertexPositionSize,
                VertexSize = vertexSize,
                LODIndex = lodID,
                VertexCount = vertexCount,
                FaceCount = faceCount,
                VertexOffset = 0,
                FaceOffset = faceOffset,
                PartOffset = (int)partOffset,
                FaceIndexSize = faceIndexSize,
                MaterialIndex = i,
                Min = new(mix, miy, miz),
                Max = new(max, may, maz),
                Center = new(cx, cy, cz),
                Scale = cr,
                Attributes = attributes,
            });
        }

        for (int l = 0; l < lodCount; l++)
        {
            var variant = variants[0]; // TODO: Support other variants, currently we just export the first one which is usually the default
            var lodToExport = lods[l];
            var lodLayout = lodToExport.Layout;
            var lodMeshGroup = scene.RootNode.AddNode(new Group($"LOD_{l}"));

            reader.BaseStream.Position = lodToExport.Layout.FileOffset;

            var vertexBytes = reader.ReadBytes(lodToExport.Layout.VertexByteCount);
            var vertexOffset2 = reader.BaseStream.Position;
            var positionOnlyVertexBytes = reader.ReadBytes(lodToExport.Layout.PositionOnlyVertexByteCount);
            var indexBytes = reader.ReadBytes(lodToExport.Layout.IndexByteCount);

            var vertexReader = new SpanReader(vertexBytes);
            var vertexPositionsReader = new SpanReader(positionOnlyVertexBytes);
            var faceReader = new SpanReader(indexBytes);

            foreach (var primitive in lodToExport.Primitives)
            {
                var vertexMap = new int[primitive.VertexCount];

                // Required
                var positionsAttribute = primitive.Attributes.First(x => x.Usage == 0);
                var normalsAttribute = primitive.Attributes.First(x => x.Usage == 1);
                var texCoordsAttribute = primitive.Attributes.First(x => x.Usage == 2);

                // Bind vertex attributes to the buffers, the game can technically store attributes in any way it wants
                // it can also store multiple weight buffers, etc.
                var buffer = new MeshPrimitiveBuffer
                {
                    Primitive = primitive,
                    PositionBuffer = new MeshAttributeBuffer()
                    {
                        Buffer = positionsAttribute.PositionAttribute ? positionOnlyVertexBytes : vertexBytes,
                        Attribute = positionsAttribute,
                        Stride = positionsAttribute.PositionAttribute ? primitive.VertexPositionSize : primitive.VertexSize
                    },
                    NormalBuffer = new MeshAttributeBuffer()
                    {
                        Buffer = normalsAttribute.PositionAttribute ? positionOnlyVertexBytes : vertexBytes,
                        Attribute = normalsAttribute,
                        Stride = normalsAttribute.PositionAttribute ? primitive.VertexPositionSize : primitive.VertexSize
                    },
                    UVBuffer = new MeshAttributeBuffer()
                    {
                        Buffer = texCoordsAttribute.PositionAttribute ? positionOnlyVertexBytes : vertexBytes,
                        Attribute = texCoordsAttribute,
                        Stride = texCoordsAttribute.PositionAttribute ? primitive.VertexPositionSize : primitive.VertexSize
                    }
                };

                // Weights are variable and may not be present, they are also spread across
                // multiple attributes.
                foreach (var indicesAttribute in primitive.Attributes.Where(x => x.Usage == 5))
                {
                    buffer.BlendIndicesBuffers.Add(new()
                    {
                        Buffer = indicesAttribute.PositionAttribute ? positionOnlyVertexBytes : vertexBytes,
                        Attribute = indicesAttribute,
                        Stride = indicesAttribute.PositionAttribute ? primitive.VertexPositionSize : primitive.VertexSize
                    });
                }
                foreach (var weightsAttribute in primitive.Attributes.Where(x => x.Usage == 6))
                {
                    buffer.BlendWeightsBuffers.Add(new()
                    {
                        Buffer = weightsAttribute.PositionAttribute ? positionOnlyVertexBytes : vertexBytes,
                        Attribute = weightsAttribute,
                        Stride = weightsAttribute.PositionAttribute ? primitive.VertexPositionSize : primitive.VertexSize
                    });
                }


                var mesh = new Mesh
                {
                    Positions   = new DataBuffer<float>(primitive.VertexCount, 1, 3),
                    Normals     = new DataBuffer<float>(primitive.VertexCount, 1, 3),
                    UVLayers    = new DataBuffer<float>(primitive.VertexCount, 1, 2),
                    FaceIndices = new DataBuffer<int>(primitive.FaceCount, 1, 1),
                    Materials = [allMaterials[variant.MaterialIndices[primitive.MaterialIndex]]]
                };

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

                lodMeshGroup.AddNode(mesh);
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

    /// <inheritdoc/>
    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return true;
    }
}