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
            var chunk = buffer.Slice(position + ChunkHeaderSize, size);

            if (tag == BankHeaderTag)
            {
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

        for (var i = 0; i + MediaIndexEntrySize <= mediaIndex.Length; i += MediaIndexEntrySize)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(mediaIndex[i..]);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(mediaIndex[(i + 4)..]);
            var length = BinaryPrimitives.ReadInt32LittleEndian(mediaIndex[(i + 8)..]);

            bank.Media.Add(new WwiseEmbeddedMedia(id, mediaData.Slice(offset, length).ToArray()));
        }

        bank.ReadHierarchy(hierarchy);

        return bank;
    }

    private void ReadHierarchy(ReadOnlySpan<byte> hierarchy)
    {
        if (hierarchy.IsEmpty)
            return;

        var objectCount = BinaryPrimitives.ReadInt32LittleEndian(hierarchy);
        var position = sizeof(int);

        for (var i = 0; i < objectCount; i++)
        {
            var type = hierarchy[position];
            var size = BinaryPrimitives.ReadInt32LittleEndian(hierarchy[(position + 1)..]);
            var body = hierarchy.Slice(position + HierarchyObjectHeaderSize, size);
            var id = BinaryPrimitives.ReadUInt32LittleEndian(body);

            position += HierarchyObjectHeaderSize + size;

            switch (type)
            {
                case 2:
                    ReadSound(id, body);
                    break;
                case 3:
                    Actions.Add(new WwiseAction(id, BinaryPrimitives.ReadUInt16LittleEndian(body[4..]), BinaryPrimitives.ReadUInt32LittleEndian(body[6..])));
                    break;
                case 4:
                    ReadEvent(id, body);
                    break;
                case 5 or 6 or 7 or 9:
                    Nodes.Add(new WwiseNode(id, (WwiseObjectType)type, ReadNodeParent(body, sizeof(uint)), []));
                    break;
                case 10 or 12 or 13:
                    Nodes.Add(new WwiseNode(id, (WwiseObjectType)type, ReadNodeParent(body, sizeof(uint) + sizeof(byte)), []));
                    break;
                case 11:
                    ReadMusicTrack(id, body);
                    break;
            }
        }
    }

    private void ReadSound(uint id, ReadOnlySpan<byte> body)
    {
        var sourceId = BinaryPrimitives.ReadUInt32LittleEndian(body[9..]);
        var nodeStart = ReadSourceEnd(body, sizeof(uint));

        Nodes.Add(new WwiseNode(id, WwiseObjectType.Sound, ReadNodeParent(body, nodeStart), [sourceId]));
    }

    private void ReadEvent(uint id, ReadOnlySpan<byte> body)
    {
        var actionCount = body[sizeof(uint)];
        var actionIds = new uint[actionCount];

        for (var i = 0; i < actionCount; i++)
            actionIds[i] = BinaryPrimitives.ReadUInt32LittleEndian(body[(sizeof(uint) + sizeof(byte) + i * sizeof(uint))..]);

        Events.Add(new WwiseEvent(id, actionIds));
    }

    private void ReadMusicTrack(uint id, ReadOnlySpan<byte> body)
    {
        var sourceCount = BinaryPrimitives.ReadInt32LittleEndian(body[sizeof(uint)..]);
        var sourceIds = new uint[sourceCount];
        var position = sizeof(uint) + sizeof(int);

        for (var i = 0; i < sourceCount; i++)
        {
            sourceIds[i] = BinaryPrimitives.ReadUInt32LittleEndian(body[(position + 5)..]);
            position = ReadSourceEnd(body, position);
        }

        var playlistCount = BinaryPrimitives.ReadInt32LittleEndian(body[(position + sizeof(byte))..]);

        position += sizeof(byte) + sizeof(int) + sizeof(int) + playlistCount * PlaylistItemSize;

        if (playlistCount > 0)
        {
            var automationCount = BinaryPrimitives.ReadInt32LittleEndian(body[position..]);

            position += sizeof(int);

            for (var i = 0; i < automationCount; i++)
            {
                var pointCount = BinaryPrimitives.ReadInt32LittleEndian(body[(position + 2 * sizeof(int))..]);

                position += 3 * sizeof(int) + pointCount * AutomationPointSize;
            }
        }

        Nodes.Add(new WwiseNode(id, WwiseObjectType.MusicTrack, ReadNodeParent(body, position), sourceIds));
    }

    private static int ReadSourceEnd(ReadOnlySpan<byte> body, int sourceStart)
    {
        var pluginId = BinaryPrimitives.ReadUInt32LittleEndian(body[sourceStart..]);
        var sourceEnd = sourceStart + BankSourceSize;

        if ((pluginId & 0xF) == SourcePluginType)
            sourceEnd += sizeof(int) + BinaryPrimitives.ReadInt32LittleEndian(body[sourceEnd..]);

        return sourceEnd;
    }

    private static uint ReadNodeParent(ReadOnlySpan<byte> body, int nodeStart)
    {
        const int effectSize = 6;
        const int effectHeaderSize = 2;
        const int trailerSize = 2 + sizeof(uint);

        var effectCount = body[nodeStart + 1];
        var parentOffset = nodeStart + effectHeaderSize + trailerSize;

        if (effectCount > 0)
            parentOffset += sizeof(byte) + effectCount * effectSize;

        return BinaryPrimitives.ReadUInt32LittleEndian(body[parentOffset..]);
    }
}
