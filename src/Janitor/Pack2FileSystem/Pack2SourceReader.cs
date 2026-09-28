using Janitor.Metadata;
using Janitor.Wwise;
using RedFox.Compression;
using RedFox.Compression.LZ4;
using RedFox.GameExtraction;
using RedFox.IO;
using RedFox.IO.FileSystem;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Janitor.Pack2FileSystem;

/// <summary>
/// Reads Alan Wake 2 Pack2 TOC files and mounts them into the <see cref="AssetManager"/>.
/// </summary>
public sealed class Pack2SourceReader : IAssetSourceReader
{
    private const int MagicValue = 0x52544F43;

    /// <summary>Gets the LZ4 compression codec used by Pack2 compressed blocks.</summary>
    public static CompressionCodec CompressedCodec { get; } = new LZ4Codec();

    /// <summary>Gets the pass-through codec used by Pack2 uncompressed blocks.</summary>
    public static CompressionCodec UncompressedCodec { get; } = new PassThroughCodec();

    /// <inheritdoc/>
    public bool CanOpen(AssetSourceRequest request)
    {
        if (request.Kind != AssetSourceKind.File)
            return false;
        if (!string.Equals(Path.GetExtension(request.Location), ".rmdtoc", StringComparison.OrdinalIgnoreCase))
            return false;
        if (request.Header.Length < 4)
            return false;
        if (MemoryMarshal.Read<uint>(request.HeaderSpan) != MagicValue)
            return false;

        return true;
    }

    /// <inheritdoc/>
    public async Task<IAssetSource> OpenAsync(AssetSourceRequest request, AssetManager assetManager, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        string location = request.Location ?? throw new InvalidOperationException("Requests must provide a file location.");

        if (!File.Exists(location))
            throw new FileNotFoundException("PAK file was not found.", location);

        // We want to give the reader to the resulting archive
        // but if we fail we need to dispose of it
        var stream = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0x1000, useAsync: false);

        try
        {
            // The game will always read 0x1000; the bytes between pack file metadata
            // and the buffer table are unused padding.
            Span<byte> headerBuffer = stackalloc byte[0x1000];
            stream.ReadExactly(headerBuffer);

            var version                = MemoryMarshal.Read<int>(headerBuffer[4..]);
            var buffers                = MemoryMarshal.Read<int>(headerBuffer[8..]);
            var bufferCount            = MemoryMarshal.Read<int>(headerBuffer[12..]);
            var referenceEntriesOffset = MemoryMarshal.Read<int>(headerBuffer[16..]);
            var referenceEntriesCount  = MemoryMarshal.Read<int>(headerBuffer[20..]);
            var directoryEntriesOffset = MemoryMarshal.Read<int>(headerBuffer[24..]);
            var directoryEntriesCount  = MemoryMarshal.Read<int>(headerBuffer[28..]);
            var fileEntriesOffset      = MemoryMarshal.Read<int>(headerBuffer[32..]);
            var fileEntriesCount       = MemoryMarshal.Read<int>(headerBuffer[36..]);
            var stringBlockOffset      = MemoryMarshal.Read<int>(headerBuffer[40..]);
            var stringBlockSize        = MemoryMarshal.Read<int>(headerBuffer[44..]);
            var classEntriesOffset     = MemoryMarshal.Read<int>(headerBuffer[48..]);
            var classEntriesCount      = MemoryMarshal.Read<int>(headerBuffer[52..]);
            var packFileMetaDataOffset = MemoryMarshal.Read<int>(headerBuffer[56..]);
            var packFileMetaDataSize   = MemoryMarshal.Read<int>(headerBuffer[60..]);
            var fileBufferInfoOffset   = MemoryMarshal.Read<int>(headerBuffer[80..]);
            var fileBufferInfoSize     = MemoryMarshal.Read<int>(headerBuffer[84..]);

            var directory = Path.GetDirectoryName(location) ?? string.Empty;
            var tocName = Path.GetFileNameWithoutExtension(location);
            var bufferTable = headerBuffer.Slice(buffers, bufferCount);

            // Calculate total decompressed size
            var totalBufferSize = 0;
            for (int i = 0; i < bufferTable.Length;)
            {
                totalBufferSize += MemoryMarshal.Read<int>(bufferTable[(i + 8)..]);
                i += 8 * ((bufferTable[i] & 15) + 2);
            }

            Span<byte> compressedBuffer = new byte[2048576];
            Span<byte> decompressedBuffer = new byte[totalBufferSize];
            var consumed = 0;

            for (int i = 0; i < bufferTable.Length;)
            {
                i += UnpackBufferInfo(bufferTable[i..], out _, out var offset, out var compressedSize, out var decompressedSize, out var codec);

                stream.Position = offset;
                stream.ReadExactly(compressedBuffer[..compressedSize]);
                codec.Decompress(compressedBuffer[..compressedSize], decompressedBuffer.Slice(consumed, decompressedSize));
                consumed += decompressedSize;
            }

#if DEBUG
            File.WriteAllBytes($"{Path.GetFileName(location)}.tocbuffer.dat", decompressedBuffer);
#endif

            var reader = new SpanReader(decompressedBuffer);
            var stringReader = new SpanReader(decompressedBuffer.Slice(stringBlockOffset, stringBlockSize));
            var toc = new Pack2Source(location);

            var directoryEntries = reader.ReadArray<DirectoryEntry>(directoryEntriesCount, directoryEntriesOffset);
            var fileEntries = reader.ReadArray<FileEntry>(fileEntriesCount, fileEntriesOffset);
            var classEntries = reader.ReadArray<ClassEntry>(classEntriesCount, classEntriesOffset);
            var packFileMetaDataBuffer = reader.Read(packFileMetaDataSize, packFileMetaDataOffset);
            var fileBufferInfoBuffer = reader.Read(fileBufferInfoSize, fileBufferInfoOffset);

            var referenceEntrySize = version >= 3 ? 24 : 16;

            for (int i = 0; i < referenceEntriesCount; i++)
            {
                var referenceEntry = reader.Read<ReferenceEntry>(referenceEntriesOffset + i * referenceEntrySize);
                var name = reader.ReadString(stringBlockOffset + referenceEntry.NameOffset, referenceEntry.NameLength);
                toc.References.Add(new(Path.Combine(directory, name)));
            }

            for (int i = 0; i < classEntries.Length; i++)
            {
                var name = stringReader.ReadString(classEntries[i].NameOffset, classEntries[i].NameLength);
                toc.Classes.Add(name);
            }

            // Step 1: Create all directory objects
            for (int i = 0; i < directoryEntries.Length; i++)
            {
                var name = reader.ReadString(stringBlockOffset + directoryEntries[i].NameOffset, directoryEntries[i].NameLength).Replace('.', '_').Replace('\\', '_').Replace('/', '_');
                toc.Directories.Add(new(name));
            }

            var nameList = assetManager.GetRequiredService<NameListService>();

            for (int i = 0; i < fileEntries.Length; i++)
            {
                var fileEntry = fileEntries[i];
                var name = stringReader.ReadString(fileEntry.NameOffset, fileEntry.NameLength);

                if (Path.GetExtension(name) == ".wem" && uint.TryParse(Path.GetFileNameWithoutExtension(name), out var mediaId) && nameList.Manager.TryGetValue(WwiseMediaNameResolver.TableName, mediaId, out var mediaName))
                    name = $"{mediaName}_{mediaId}.wem";

                var metaData = packFileMetaDataBuffer.Slice(fileEntry.MetaDataOffset, fileEntry.MetaDataSize);
                var bufInfo = fileBufferInfoBuffer.Slice(fileEntry.BufferInfoOffset, fileEntry.BufferInfoSize);

                var file = new Pack2File(toc, name, fileEntry.Size, metaData.ToArray(), bufInfo.ToArray());

                toc.AddFile(file);
            }

            // Step 2: Assign parent-child relationships
            for (int i = 0; i < directoryEntries.Length; i++)
            {
                var entry = directoryEntries[i];
                var parent = toc.Directories[i];

                for (int j = 0; j < entry.ChildDirectoryCount; j++)
                    toc.Directories[entry.FirstChildDirectoryIndex + j].MoveTo(parent);

                for (int j = 0; j < entry.ChildFileCount; j++)
                    toc.Files[entry.FirstChildFileIndex + j].MoveTo(parent);
            }

            // Step 3: Mount into VFS
            var vfs = assetManager.GetRequiredService<AssetFileSystemService>().FileSystem;
            var root = vfs.Root.CreateDirectory(tocName);

            foreach (var dir in toc.Directories)
            {
                if (dir.Parent == null)
                    dir.MoveTo(root);
            }

            var resourceTable = assetManager.GetRequiredService<ResourceTableService>().Resources;

            // Step 4: Build resource ID map
            foreach (var file in toc.Files)
            {
                if (file.TryParseMetadata<ResourceMetadata>("r::ResourceMetadata", out var resourceMetadata))
                    resourceTable[resourceMetadata.Id] = file;
            }

            // Finally, add assets for all files
            foreach (var file in toc.Files)
            {
                var asset = new Asset(file.FullPath, Path.GetExtension(file.Name), file, $"Size: 0x{file.Size:X}");
                file.Data = asset;
                toc.AddAsset(asset);
            }

#if DEBUG
            using var classWriter = new StreamWriter("classes.txt");

            foreach (var resourceClass in toc.Classes)
            {
                classWriter.WriteLine(resourceClass);
            }
#endif

            return toc;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes a single buffer table entry and returns its size in bytes.
    /// </summary>
    /// <param name="tableEntry">The raw table entry bytes.</param>
    /// <param name="blobIndex">Index into the TOC's reference/blob table.</param>
    /// <param name="offset">Byte offset into the blob file.</param>
    /// <param name="compressedSize">Compressed byte count.</param>
    /// <param name="decompressedSize">Decompressed byte count.</param>
    /// <param name="codec">The codec to use for this block.</param>
    /// <returns>The number of bytes consumed from <paramref name="tableEntry"/>.</returns>
    /// <exception cref="NotImplementedException">Thrown for unrecognised entry types.</exception>
    public static int UnpackBufferInfo(ReadOnlySpan<byte> tableEntry, out int blobIndex, out long offset, out int compressedSize, out int decompressedSize, out CompressionCodec codec)
    {
        var packed = MemoryMarshal.Read<long>(tableEntry);
        offset    = packed >> 24;
        blobIndex = tableEntry[1];

        switch (tableEntry[0])
        {
            case 0:
                decompressedSize = MemoryMarshal.Read<int>(tableEntry[8..]);
                compressedSize   = decompressedSize;
                codec            = UncompressedCodec;
                break;
            case 16:
                decompressedSize = MemoryMarshal.Read<int>(tableEntry[8..]);
                compressedSize   = MemoryMarshal.Read<int>(tableEntry[12..]);
                codec            = CompressedCodec;
                break;
            default:
                throw new NotImplementedException($"Unsupported Pack2 buffer entry type: 0x{tableEntry[0]:X2}");
        }

        return 8 * ((tableEntry[0] & 15) + 2);
    }

#pragma warning disable CS0649 // fields assigned via MemoryMarshal.Read
    private struct DirectoryEntry
    {
        public int TreeLevel;
        public int FirstChildDirectoryIndex;
        public int ChildDirectoryCount;
        public int FirstChildFileIndex;
        public int ChildFileCount;
        public int NameOffset;
        public int NameLength;
    }

    private struct FileEntry
    {
        public int BufferInfoOffset;
        public int BufferInfoSize;
        public int ID;
        public int NameOffset;
        public int NameLength;
        public int Size;
        public int MetaDataOffset;
        public int MetaDataSize;
    }

    private struct ReferenceEntry
    {
        public int NameOffset;
        public int NameLength;
        public long Hash;
    }

    private struct ClassEntry
    {
        public int NameOffset;
        public int NameLength;
    }
#pragma warning restore CS0649
}
