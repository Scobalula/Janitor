using Janitor.Metadata;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Janitor.AssetHandling;

public class ClipResourceHandler : IAssetHandler
{
    public bool CanHandle(Asset asset)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".binanimclip", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        var nameTable = context.AssetManager.GetRequiredService<NameListService>().Manager;

        using var reader = new BinaryReader(file.Open());

        reader.BaseStream.Position = (long)((ulong)(reader.BaseStream.Position + 19) & 0xFFFFFFFFFFFFFFF0);

        var literalStart = reader.BaseStream.Position;
        var startOffset = literalStart + reader.ReadUInt32();
        var endOffset = startOffset + reader.ReadInt32();
        var offsetCount = reader.ReadInt32();
        var structOffsets = reader.ReadStructArray<uint>(offsetCount);
        var offsets = new long[offsetCount];

        for (var i = 0; i < offsets.Length; i++)
        {
            reader.BaseStream.Position = startOffset + structOffsets[i];
            offsets[i] = startOffset + reader.ReadInt64();
        }

        var boneCount = reader.ReadInt32(startOffset + 4);
        var hashesOffset = ReadRelativeOffset(reader, startOffset + 128);

        var hashes = reader.ReadStructArray<uint>(boneCount, hashesOffset);
        var trackNames = new string[boneCount];

        var skeleton = new SkeletonAnimation($"Anim", boneCount, TransformType.Absolute);

        for (var i = 0; i < hashes.Length; i++)
        {
            if (nameTable.TryGetValue("BoneTable", hashes[i], out var boneName))
            {
                trackNames[i] = boneName;
            }
            else
            {
                trackNames[i] = $"bone_{hashes[i]:X}";
            }

            skeleton.Tracks.Add(new(trackNames[i]));
        }

        return new AssetReadResult
        {
            Asset = asset,
            Data = null,
            Handler = this,
        };
    }

    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return true;
    }

    public static long ReadRelativeOffset(BinaryReader reader, long offset)
    {
        reader.BaseStream.Position = offset;
        return offset + reader.ReadInt32();
    }
}
