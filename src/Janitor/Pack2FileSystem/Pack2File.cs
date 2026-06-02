using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RedFox.IO;
using RedFox.IO.FileSystem;
using RedFox.GameExtraction;
using Janitor.Metadata;

namespace Janitor.Pack2FileSystem;

/// <summary>
/// A class to hold an instance of a Pack file.
/// </summary>
public class Pack2File(Pack2Source owner, string name, long size, byte[] metaDataBuffer, byte[] bufferInfo) : VirtualFile(name, size)
{
    /// <summary>
    /// Gets the owner of this pack file.
    /// </summary>
    public Pack2Source Owner { get; } = owner;

    /// <summary>
    /// Gets the metadata buffer.
    /// </summary>
    public ReadOnlyMemory<byte> MetaData { get; } = metaDataBuffer;

    /// <summary>
    /// Gets the buffer info buffer.
    /// </summary>
    public ReadOnlyMemory<byte> BufferInfo { get; } = bufferInfo;

    public byte[] ReadAllBytes()
    {
        var finalBuffer = new byte[(int)Size];
        var compressedBuffer = new byte[(int)Size];
        var consumed = 0;

        var finalBufferSpan = finalBuffer.AsSpan();

        for (int c = 0; c < BufferInfo.Length;)
        {
            c += Pack2SourceReader.UnpackBufferInfo(BufferInfo[c..].Span, out int blobIndex, out var offset, out var compressedSize, out var decompressedSize, out var codec);

            if (compressedSize > compressedBuffer.Length)
                compressedBuffer = new byte[compressedSize];

            var localCompressedBuffer = compressedBuffer.AsSpan()[..compressedSize];

            Owner.References[blobIndex].Read(offset, localCompressedBuffer[..]);

            codec.Decompress(localCompressedBuffer[..compressedSize], finalBufferSpan.Slice(consumed, decompressedSize));


            consumed += decompressedSize;
        }

        return finalBuffer;
    }

    /// <inheritdoc/>
    public override Stream Open()
    {
        return new MemoryStream(ReadAllBytes());
    }

    /// <inheritdoc/>
    public override void WriteAllBytes(string filePath)
    {
        base.WriteAllBytes(filePath);
        File.WriteAllBytes(filePath + ".meta", MetaData.ToArray());
    }

    public bool TryParseMetadata<T>(string metaDataName, [NotNullWhen(true)] out T? metaData) where T : IMetadata, new()
    {
        metaData = default;

        if (MetaData.Length < 8)
            return false;

        // Pack File Meta Data
        var reader = new SpanReader(MetaData.Span);

        if (reader.Read<uint>() != 0x504B4D44)
            throw new NotSupportedException("Unsupported file");

        var c1 = reader.Read<int>();
        var dataStart = c1 * 8 + 8;

        for (int i = 0; i < c1; i++)
        {
            var type = reader.Read<int>();
            var start = dataStart + reader.Read<ushort>();
            var end = dataStart + reader.Read<ushort>();

            if (Owner.Classes[type] == metaDataName)
            {
                metaData = new T();
                metaData.Parse(MetaData.Span[start..end]);

                return true;
            }
        }

        metaData = default;
        return false;
    }

    public bool HasMetaData(string v)
    {
        if (MetaData.Length < 8)
            return false;

        // Pack File Meta Data
        var reader = new SpanReader(MetaData.Span);

        if (reader.Read<uint>() != 0x504B4D44)
            throw new NotSupportedException("Unsupported file");

        var c1 = reader.Read<int>();

        for (int i = 0; i < c1; i++)
        {
            var type = reader.Read<int>();
            reader.Position += 4;

            if (Owner.Classes[type] == v)
            {
                return true;
            }
        }

        return false;
    }
}
