using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public class SkeletonResourceHandler : IAssetHandler
{
    public bool CanHandle(Asset asset)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".binskeleton", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        var skeletonGroup = new Group(asset.Name);

        using var reader = new BinaryReader(file.Open());

        reader.BaseStream.Position = (long)(((ulong)reader.BaseStream.Position + 19) & 0xFFFFFFFFFFFFFFF0);

        var literalStart = reader.BaseStream.Position;
        var startOffset = literalStart + reader.ReadUInt32();
        var endOffset = startOffset + reader.ReadInt32();
        var offsetCount = reader.ReadInt32();
        var structOffsets = reader.ReadStructArray<uint>(offsetCount);
        var offsets = new long[offsetCount];

        reader.BaseStream.Position = startOffset;

        var boneCount = reader.ReadInt32();
        _ = reader.ReadSingle();

        for (var i = 0; i < offsets.Length; i++)
        {
            reader.BaseStream.Position = startOffset + structOffsets[i];
            offsets[i] = startOffset + reader.ReadInt64();
        }

        var bindPoseOffset = offsets[0];
        var parentIndicesOffset = offsets[1];
        var boneInfoOffset = offsets[2];

        var bones = new SkeletonBone[boneCount];

        reader.BaseStream.Position = bindPoseOffset;

        for (var i = 0; i < boneCount; i++)
        {
            var rotation = reader.ReadStruct<Quaternion>();
            var position = reader.ReadStruct<Vector4>() * 100.0f;

            var bone = new SkeletonBone($"bone_{i}");
            bone.BindTransform.LocalRotation = rotation;
            bone.BindTransform.LocalPosition = new Vector3(position.X, position.Y, position.Z);
            bone.BindTransform.Scale = Vector3.One;

            bones[i] = bone;
        }

        reader.BaseStream.Position = parentIndicesOffset;

        skeletonGroup.LinkHierarchyUnsafe(bones, reader.ReadStructArray<short>(boneCount));

        reader.BaseStream.Position = boneInfoOffset;

        for (var i = 0; i < boneCount; i++)
        {
            bones[i].UserId = reader.ReadUInt32();
            bones[i].Name = $"bone_{bones[i].UserId}";
        }

        // I don't think the game actually uses the extended info, but it contains the bone names so we need to read it to get them.
        // I think internally the game uses the hashed bone names for lookups, and the extended info is only used for debugging purposes?
        var extendedInfoOffset = (long)(((ulong)endOffset + 15) & 0xFFFFFFFFFFFFFFF0);

        reader.BaseStream.Position = extendedInfoOffset;
        reader.BaseStream.Position = reader.ReadUInt32() + extendedInfoOffset;

        var startOfNames = reader.BaseStream.Position;
        var nameTableOffset = reader.ReadInt64();
        var nameTableCount = reader.ReadInt32();
        var namePointers = reader.ReadStructArray<long>(nameTableCount, startOfNames + nameTableOffset);

        for (var i = 0; i < Math.Min(nameTableCount, boneCount); i++)
            bones[i].Name = reader.ReadUTF8NullTerminatedString(namePointers[i] + startOfNames);

        // Store the original table for skinning on an attribute.
        skeletonGroup.SetAttribute("OriginalTable", bones);

        return new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = skeletonGroup
        };
    }

    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
