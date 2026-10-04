using RedFox.IO;
using System.Numerics;

namespace Janitor.Animation;

/// <summary>
/// Reads the clips an animation graph (.binanimgraph) samples as additive layers, along with the base poses stored for them.
/// </summary>
public sealed class AnimationGraph
{
    /// <summary>
    /// The oldest .binanimgraph version supported.
    /// </summary>
    public const int MinimumVersion = 119;

    /// <summary>
    /// The newest .binanimgraph version supported.
    /// </summary>
    public const int MaximumVersion = 177;

    /// <summary>
    /// The version that dropped 8 bytes before the rigs, narrowed the animation set count to a byte, shared clip node entries
    /// between rigs and renumbered the node types.
    /// </summary>
    public const int CompactDefinitionVersion = 150;

    /// <summary>
    /// The version that dropped another 8 bytes before the rigs and inserted node types before the progressive and speed clip nodes.
    /// </summary>
    public const int TrimmedDefinitionVersion = 177;

    private const int RelocationInfoOffset = 16;

    private const int NodeSize = 12;

    private const int RigSize = 24;

    private const int TransformSize = 32;

    /// <summary>
    /// Gets the resource IDs of every clip the graph samples.
    /// </summary>
    public IReadOnlyList<ulong> ClipResourceIds { get; }

    /// <summary>
    /// Gets the clips the graph samples as additive layers.
    /// </summary>
    public IReadOnlyList<AdditiveClip> AdditiveClips { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationGraph"/> class by reading the graph definition.
    /// </summary>
    /// <param name="reader">The reader to read the graph from, positioned anywhere.</param>
    /// <exception cref="NotSupportedException">Thrown when the graph version is not supported.</exception>
    public AnimationGraph(BinaryReader reader)
    {
        // * The definition is the first relocated block, every pointer in it is a 64 bit offset from its start.
        // * Clip nodes hold a {clip index, base pose index} pair per animation set, older versions also store a set of pairs per rig.
        // * Additive clip nodes subtract the base pose of the rig from their samples, the inverse of which is stored per rig bone.
        var version = reader.ReadInt32(0);

        if (!IsSupported(version))
            throw new NotSupportedException($"Animation graph version {version}");

        var isCompact = version >= CompactDefinitionVersion;
        var offset = RelocationInfoOffset + reader.ReadUInt32(RelocationInfoOffset);
        var fieldOffset = version >= TrimmedDefinitionVersion ? -8 : isCompact ? 0 : 8;
        var nodeCount = reader.ReadUInt16(offset);
        var nodesOffset = ReadPointer(reader, offset, offset + 8);
        var nodeDataOffset = ReadPointer(reader, offset, offset + 16);
        var rigsOffset = ReadPointer(reader, offset, offset + fieldOffset + 192);
        var rigDataOffset = ReadPointer(reader, offset, offset + fieldOffset + 200);
        var clipIds = reader.ReadStructArray<ulong>((int)reader.ReadInt64(offset + fieldOffset + 232), ReadPointer(reader, offset, offset + fieldOffset + 224)).ToArray();
        var setCount = Math.Max(1, isCompact ? reader.ReadByte(offset + fieldOffset + 272) : (int)reader.ReadUInt16(offset + fieldOffset + 280));
        var rigCount = reader.ReadUInt16(offset + fieldOffset + 296);
        var additiveClips = new List<AdditiveClip>();
        var uses = new HashSet<(int Clip, int Rig, int BasePose)>();

        for (var node = 0; node < nodeCount; node++)
        {
            var nodeOffset = nodeDataOffset + reader.ReadUInt32(nodesOffset + node * NodeSize + 4);
            var entriesOffset = ReadAdditiveEntries(reader, offset, nodeOffset, reader.ReadByte(nodesOffset + node * NodeSize), version);

            if (entriesOffset < 0)
                continue;

            for (var rig = 0; rig < rigCount; rig++)
            {
                for (var set = 0; set < setCount; set++)
                {
                    var entryOffset = entriesOffset + (set + (isCompact ? 0 : rig * setCount)) * 4;
                    var clip = reader.ReadUInt16(entryOffset);
                    var basePose = reader.ReadUInt16(entryOffset + 2);

                    if (uses.Add((clip, rig, basePose)))
                        additiveClips.Add(new AdditiveClip(clipIds[clip], ReadInverseBasePose(reader, offset, rigsOffset + rig * RigSize, rigDataOffset + rig * RigSize, basePose)));
                }
            }
        }

        ClipResourceIds = clipIds;
        AdditiveClips = additiveClips;
    }

    /// <summary>
    /// Gets whether the given .binanimgraph version can be read.
    /// </summary>
    /// <param name="version">The .binanimgraph version.</param>
    /// <returns><see langword="true"/> when the version is supported, otherwise <see langword="false"/>.</returns>
    public static bool IsSupported(int version) => version is >= MinimumVersion and <= MaximumVersion;

    private static long ReadAdditiveEntries(BinaryReader reader, long offset, long nodeOffset, byte nodeType, int version)
    {
        // Returns the position of the {clip index, base pose index} pairs of additive clip, progressive clip and speed clip nodes, or -1 for every other node.
        var (entriesField, isRelative, flagsOffset, additiveFlag) = version switch
        {
            >= TrimmedDefinitionVersion => nodeType switch { 9 => (0, true, 11, 2), 24 => (0, true, 20, 4), 33 => (0, true, 14, 2), _ => (0, false, 0, 0) },
            >= CompactDefinitionVersion => nodeType switch { 9 => (0, true, 11, 2), 23 => (0, true, 20, 4), 31 => (0, true, 14, 2), _ => (0, false, 0, 0) },
            _ => nodeType switch { 4 => (8, true, 6, 8), 27 => (0, false, 24, 4), 32 => (8, false, 6, 2), _ => (0, false, 0, 0) },
        };

        if ((reader.ReadByte(nodeOffset + flagsOffset) & additiveFlag) == 0)
            return -1;

        return isRelative ? nodeOffset + entriesField + reader.ReadInt32(nodeOffset + entriesField) : ReadPointer(reader, offset, nodeOffset + entriesField);
    }

    private static Dictionary<uint, (Quaternion Rotation, Vector3 Translation)> ReadInverseBasePose(BinaryReader reader, long offset, long rigOffset, long rigDataOffset, int basePose)
    {
        var boneCount = reader.ReadInt32(rigOffset);
        var boneHashes = reader.ReadStructArray<uint>(boneCount, ReadPointer(reader, offset, rigOffset + 8)).ToArray();
        var posesOffset = ReadPointer(reader, offset, rigDataOffset + 16) + (long)basePose * boneCount * TransformSize;
        var inverseBasePose = new Dictionary<uint, (Quaternion Rotation, Vector3 Translation)>(boneCount);

        for (var bone = 0; bone < boneCount; bone++)
            inverseBasePose.TryAdd(boneHashes[bone], (reader.ReadStruct<Quaternion>(posesOffset + bone * TransformSize), reader.ReadStruct<Vector3>(posesOffset + bone * TransformSize + 16)));

        return inverseBasePose;
    }

    private static long ReadPointer(BinaryReader reader, long offset, long field) => offset + reader.ReadInt64(field);
}
