using System.Buffers.Binary;

namespace Janitor.Wwise;

/// <summary>
/// A parsed Wwise sound bank (.bnk), exposing the header details, any media embedded in its data section
/// and the parts of its hierarchy needed to trace media back to the events that play it.
/// </summary>
public sealed class WwiseBank
{
    private const uint BankHeaderTag = 0x44484B42;
    private const uint MediaIndexTag = 0x58444944;
    private const uint MediaDataTag = 0x41544144;
    private const uint HierarchyTag = 0x43524948;
    private const int ChunkHeaderSize = 8;
    private const int MediaIndexEntrySize = 12;
    private const int HierarchyObjectHeaderSize = 5;
    private const int BankSourceSize = 18;
    private const int PlaylistItemSize = 48;
    private const int AutomationPointSize = 12;
    private const int SourcePluginType = 2;
    private const int SoundNodeExtraSize = sizeof(uint);

    /// <summary>
    /// Gets the version of the Wwise sound engine the bank was built for.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>
    /// Gets the identifier of the bank.
    /// </summary>
    public uint Id { get; private set; }

    /// <summary>
    /// Gets the media files embedded in the bank, which is empty when the bank only holds objects and its media is streamed from stand-alone .wem files.
    /// </summary>
    public List<WwiseEmbeddedMedia> Media { get; } = [];

    /// <summary>
    /// Gets the sounds, containers, segments and music tracks of the bank.
    /// </summary>
    public List<WwiseNode> Nodes { get; } = [];

    /// <summary>
    /// Gets the events of the bank.
    /// </summary>
    public List<WwiseEvent> Events { get; } = [];

    /// <summary>
    /// Gets the event actions of the bank.
    /// </summary>
    public List<WwiseAction> Actions { get; } = [];

    /// <summary>
    /// Parses a sound bank from its raw bytes.
    /// </summary>
    /// <param name="buffer">The bytes of the .bnk file.</param>
    /// <returns>The parsed bank.</returns>
    /// <exception cref="NotSupportedException">Thrown when the buffer does not start with a bank header chunk.</exception>
    /// <exception cref="InvalidDataException">Thrown when a bank chunk has an invalid size or header.</exception>
    public static WwiseBank Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < ChunkHeaderSize + 8 || BinaryPrimitives.ReadUInt32LittleEndian(buffer) != BankHeaderTag)
            throw new NotSupportedException("Buffer is not a Wwise sound bank.");

        var bank = new WwiseBank();
        var mediaIndex = ReadOnlySpan<byte>.Empty;
        var mediaData = ReadOnlySpan<byte>.Empty;
        var hierarchy = ReadOnlySpan<byte>.Empty;
        var position = 0;

        while (position + ChunkHeaderSize <= buffer.Length)
        {
            var tag = BinaryPrimitives.ReadUInt32LittleEndian(buffer[position..]);
            var size = BinaryPrimitives.ReadInt32LittleEndian(buffer[(position + 4)..]);

            if (size < 0 || size > buffer.Length - position - ChunkHeaderSize)
                throw new InvalidDataException($"Wwise chunk at offset 0x{position:X} has an invalid size of {size} bytes.");

            var chunk = buffer.Slice(position + ChunkHeaderSize, size);

            if (tag == BankHeaderTag)
            {
                if (chunk.Length < 8)
                    throw new InvalidDataException("Wwise bank header chunk is shorter than its required fields.");

                bank.Version = BinaryPrimitives.ReadUInt32LittleEndian(chunk);
                bank.Id = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            }
            else if (tag == MediaIndexTag)
            {
                mediaIndex = chunk;
            }
            else if (tag == MediaDataTag)
            {
                mediaData = chunk;
            }
            else if (tag == HierarchyTag)
            {
                hierarchy = chunk;
            }

            position += ChunkHeaderSize + size;
        }

        for (var i = 0; i <= mediaIndex.Length - MediaIndexEntrySize; i += MediaIndexEntrySize)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(mediaIndex[i..]);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(mediaIndex[(i + 4)..]);
            var length = BinaryPrimitives.ReadInt32LittleEndian(mediaIndex[(i + 8)..]);

            if (offset < 0 || length < 0 || offset > mediaData.Length || length > mediaData.Length - offset)
                continue;

            bank.Media.Add(new WwiseEmbeddedMedia(id, mediaData.Slice(offset, length).ToArray()));
        }

        bank.ReadHierarchy(hierarchy);

        return bank;
    }

    private void ReadHierarchy(ReadOnlySpan<byte> hierarchy)
    {
        if (hierarchy.Length < sizeof(int))
            return;

        var objectCount = BinaryPrimitives.ReadInt32LittleEndian(hierarchy);
        var position = sizeof(int);

        if (objectCount < 0 || objectCount > (hierarchy.Length - position) / HierarchyObjectHeaderSize)
            return;

        var hierarchyIds = ReadHierarchyNodeIds(hierarchy, objectCount);
        var mediaIds = Media.Select(media => media.Id).ToHashSet();

        for (var i = 0; i < objectCount && position <= hierarchy.Length - HierarchyObjectHeaderSize; i++)
        {
            var type = hierarchy[position];
            var size = BinaryPrimitives.ReadInt32LittleEndian(hierarchy[(position + 1)..]);

            if (size < sizeof(uint) || size > hierarchy.Length - position - HierarchyObjectHeaderSize)
                break;

            var body = hierarchy.Slice(position + HierarchyObjectHeaderSize, size);
            var id = BinaryPrimitives.ReadUInt32LittleEndian(body);

            position += HierarchyObjectHeaderSize + size;

            switch (type)
            {
                case 2:
                    ReadSound(id, body);
                    break;
                case 3:
                    if (body.Length >= sizeof(uint) + sizeof(ushort) + sizeof(uint))
                        Actions.Add(new WwiseAction(id, BinaryPrimitives.ReadUInt16LittleEndian(body[4..]), BinaryPrimitives.ReadUInt32LittleEndian(body[6..])));
                    break;
                case 4:
                    ReadEvent(id, body);
                    break;
                case 5 or 6 or 7 or 9:
                    if (TryReadNodeParent(body, sizeof(uint), out var parentId))
                        Nodes.Add(new WwiseNode(id, (WwiseObjectType)type, parentId, []));
                    break;
                case 10 or 12 or 13:
                    if (TryReadNodeParent(body, sizeof(uint) + sizeof(byte), out parentId))
                        Nodes.Add(new WwiseNode(id, (WwiseObjectType)type, parentId, []));
                    break;
                case 11:
                    ReadMusicTrack(id, body, hierarchyIds, mediaIds);
                    break;
            }
        }
    }

    private void ReadSound(uint id, ReadOnlySpan<byte> body)
    {
        if (body.Length < BankSourceSize)
            return;

        var pluginId = BinaryPrimitives.ReadUInt32LittleEndian(body[sizeof(uint)..]);
        var sourceId = BinaryPrimitives.ReadUInt32LittleEndian(body[9..]);
        if (!TryReadSourceEnd(body, sizeof(uint), out var nodeStart))
            return;

        if ((pluginId & 0xF) == SourcePluginType)
            nodeStart += SoundNodeExtraSize;

        if (TryReadNodeParent(body, nodeStart, out var parentId))
            Nodes.Add(new WwiseNode(id, WwiseObjectType.Sound, parentId, [sourceId]));
    }

    private void ReadEvent(uint id, ReadOnlySpan<byte> body)
    {
        if (body.Length < sizeof(uint) + sizeof(byte))
            return;

        var actionCount = body[sizeof(uint)];

        if (actionCount > (body.Length - sizeof(uint) - sizeof(byte)) / sizeof(uint))
            return;

        var actionIds = new uint[actionCount];

        for (var i = 0; i < actionCount; i++)
            actionIds[i] = BinaryPrimitives.ReadUInt32LittleEndian(body[(sizeof(uint) + sizeof(byte) + i * sizeof(uint))..]);

        Events.Add(new WwiseEvent(id, actionIds));
    }

    private void ReadMusicTrack(uint id, ReadOnlySpan<byte> body, HashSet<uint> hierarchyIds, HashSet<uint> mediaIds)
    {
        if (TryReadMusicTrack(id, body, out var node))
        {
            Nodes.Add(node);
            return;
        }

        var sourceIds = new List<uint>();
        var seenSourceIds = new HashSet<uint>();

        for (var position = sizeof(uint); position <= body.Length - sizeof(uint); position++)
        {
            var sourceId = BinaryPrimitives.ReadUInt32LittleEndian(body[position..]);

            if (mediaIds.Contains(sourceId) && seenSourceIds.Add(sourceId))
                sourceIds.Add(sourceId);
        }

        var parentId = 0u;

        for (var position = body.Length - sizeof(uint); position >= sizeof(uint); position--)
        {
            var candidate = BinaryPrimitives.ReadUInt32LittleEndian(body[position..]);

            if (candidate != id && hierarchyIds.Contains(candidate))
            {
                parentId = candidate;
                break;
            }
        }

        if (parentId != 0 || sourceIds.Count != 0)
            Nodes.Add(new WwiseNode(id, WwiseObjectType.MusicTrack, parentId, [.. sourceIds]));
    }

    private static bool TryReadMusicTrack(uint id, ReadOnlySpan<byte> body, out WwiseNode node)
    {
        node = null!;

        if (body.Length < sizeof(uint) + sizeof(int))
            return false;

        var sourceCount = BinaryPrimitives.ReadInt32LittleEndian(body[sizeof(uint)..]);

        if (sourceCount < 0 || sourceCount > (body.Length - sizeof(uint) - sizeof(int)) / BankSourceSize)
            return false;

        var sourceIds = new uint[sourceCount];
        var position = sizeof(uint) + sizeof(int);

        for (var i = 0; i < sourceCount; i++)
        {
            if (position > body.Length - BankSourceSize)
                return false;

            sourceIds[i] = BinaryPrimitives.ReadUInt32LittleEndian(body[(position + 5)..]);

            if (!TryReadSourceEnd(body, position, out position))
                return false;
        }

        if (position > body.Length - sizeof(byte) - sizeof(int))
            return false;

        var playlistCount = BinaryPrimitives.ReadInt32LittleEndian(body[(position + sizeof(byte))..]);

        if (playlistCount < 0 || playlistCount > (body.Length - position - sizeof(byte) - 2 * sizeof(int)) / PlaylistItemSize)
            return false;

        position += sizeof(byte) + sizeof(int) + sizeof(int) + playlistCount * PlaylistItemSize;

        if (playlistCount > 0)
        {
            if (position > body.Length - sizeof(int))
                return false;

            var automationCount = BinaryPrimitives.ReadInt32LittleEndian(body[position..]);

            if (automationCount < 0 || automationCount > (body.Length - position - sizeof(int)) / (3 * sizeof(int)))
                return false;

            position += sizeof(int);

            for (var i = 0; i < automationCount; i++)
            {
                if (position > body.Length - 3 * sizeof(int))
                    return false;

                var pointCount = BinaryPrimitives.ReadInt32LittleEndian(body[(position + 2 * sizeof(int))..]);

                if (pointCount < 0 || pointCount > (body.Length - position - 3 * sizeof(int)) / AutomationPointSize)
                    return false;

                position += 3 * sizeof(int) + pointCount * AutomationPointSize;
            }
        }

        if (!TryReadNodeParent(body, position, out var parentId))
            return false;

        node = new WwiseNode(id, WwiseObjectType.MusicTrack, parentId, sourceIds);
        return true;
    }

    private static HashSet<uint> ReadHierarchyNodeIds(ReadOnlySpan<byte> hierarchy, int objectCount)
    {
        HashSet<uint> ids = [];
        var position = sizeof(int);

        for (var i = 0; i < objectCount && position <= hierarchy.Length - HierarchyObjectHeaderSize; i++)
        {
            var type = hierarchy[position];
            var size = BinaryPrimitives.ReadInt32LittleEndian(hierarchy[(position + 1)..]);

            if (size < sizeof(uint) || size > hierarchy.Length - position - HierarchyObjectHeaderSize)
                break;

            if (type is 2 or 5 or 6 or 7 or 9 or 10 or 11 or 12 or 13)
                ids.Add(BinaryPrimitives.ReadUInt32LittleEndian(hierarchy[(position + HierarchyObjectHeaderSize)..]));

            position += HierarchyObjectHeaderSize + size;
        }

        return ids;
    }

    private static bool TryReadSourceEnd(ReadOnlySpan<byte> body, int sourceStart, out int sourceEnd)
    {
        sourceEnd = 0;

        if (sourceStart < 0 || sourceStart > body.Length - BankSourceSize)
            return false;

        var pluginId = BinaryPrimitives.ReadUInt32LittleEndian(body[sourceStart..]);
        sourceEnd = sourceStart + BankSourceSize;

        if ((pluginId & 0xF) == SourcePluginType)
        {
            if (sourceEnd > body.Length - sizeof(int))
                return false;

            var pluginDataSize = BinaryPrimitives.ReadInt32LittleEndian(body[sourceEnd..]);

            if (pluginDataSize < 0 || pluginDataSize > body.Length - sourceEnd - sizeof(int))
                return false;

            sourceEnd += sizeof(int) + pluginDataSize;
        }

        return true;
    }

    private static bool TryReadNodeParent(ReadOnlySpan<byte> body, int nodeStart, out uint parentId)
    {
        const int effectSize = 6;
        const int effectHeaderSize = 2;
        const int trailerSize = 2 + sizeof(uint);

        parentId = 0;

        if (nodeStart < 0 || nodeStart > body.Length - effectHeaderSize - trailerSize)
            return false;

        var effectCount = body[nodeStart + 1];
        var parentOffset = nodeStart + effectHeaderSize + trailerSize;

        if (effectCount > 0)
            parentOffset += sizeof(byte) + effectCount * effectSize;

        if (parentOffset > body.Length - sizeof(uint))
            return false;

        parentId = BinaryPrimitives.ReadUInt32LittleEndian(body[parentOffset..]);
        return true;
    }
}
