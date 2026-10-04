using RedFox.IO;

namespace Janitor.AssetHandling;

/// <summary>
/// Describes the header of an animation clip, resolving the field layout used by each supported .binanimclip version.
/// Fields that point at arrays are stored as the absolute position of their self relative offset.
/// </summary>
public sealed class ClipHeader
{
    /// <summary>
    /// The oldest .binanimclip version supported.
    /// </summary>
    public const int MinimumVersion = 21;

    /// <summary>
    /// The newest .binanimclip version supported.
    /// </summary>
    public const int MaximumVersion = 27;

    /// <summary>
    /// The version that narrowed the sampled curve count and compression to bytes, moved the event counts down and dropped 32 bytes
    /// before the visibility fields.
    /// </summary>
    public const int CompactHeaderVersion = 25;

    /// <summary>
    /// The version that stores section boundaries in seconds rather than as fractions of the duration.
    /// </summary>
    public const int SectionTimesVersion = 27;

    /// <summary>
    /// Gets the position of the clip within the file, which 64 bit data offsets are relative to.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// Gets the number of frames.
    /// </summary>
    public int FrameCount { get; }

    /// <summary>
    /// Gets the number of animated bones.
    /// </summary>
    public int BoneCount { get; }

    /// <summary>
    /// Gets the number of curves, including those only referenced by events.
    /// </summary>
    public int CurveCount { get; }

    /// <summary>
    /// Gets the number of curves with sampled values.
    /// </summary>
    public int CompressedCurveCount { get; }

    /// <summary>
    /// Gets the number of instant events.
    /// </summary>
    public int InstantEventCount { get; }

    /// <summary>
    /// Gets the number of duration events.
    /// </summary>
    public int RangeEventCount { get; }

    /// <summary>
    /// Gets the compression of the bone, trajectory and curve data, 0 being raw samples, 1 and 2 being ACL.
    /// </summary>
    public int Compression { get; }

    /// <summary>
    /// Gets the number of bone visibility groups.
    /// </summary>
    public int VisibilityGroupCount { get; }

    /// <summary>
    /// Gets the duration of the clip in seconds.
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// Gets the number of frames per second.
    /// </summary>
    public float Framerate { get; }

    /// <summary>
    /// Gets the position of the field pointing at the bone transform data.
    /// </summary>
    public long BoneTracksField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the trajectory data, which is zero when the clip has no root motion.
    /// </summary>
    public long TrajectoryField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the curve name hashes.
    /// </summary>
    public long CurveHashesField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the curve index of each sampled curve.
    /// </summary>
    public long CurveIndicesField { get; }

    /// <summary>
    /// Gets the size, in bytes, of each sampled curve index.
    /// </summary>
    public int CurveIndexSize { get; }

    /// <summary>
    /// Gets the position of the field pointing at the sampled curve data.
    /// </summary>
    public long CurvesField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the instant events.
    /// </summary>
    public long InstantEventsField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the duration events.
    /// </summary>
    public long RangeEventsField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the per frame visibility bits of each group.
    /// </summary>
    public long VisibilityBitsField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the bones of every visibility group.
    /// </summary>
    public long VisibilityBonesField { get; }

    /// <summary>
    /// Gets the position of the field pointing at the number of bones in each visibility group.
    /// </summary>
    public long VisibilityBoneCountsField { get; }

    /// <summary>
    /// Gets the number of sections.
    /// </summary>
    public int SectionCount { get; }

    /// <summary>
    /// Gets the position of the field pointing at the section boundaries.
    /// </summary>
    public long SectionsField { get; }

    /// <summary>
    /// Gets whether section boundaries are fractions of the duration, rather than times in seconds.
    /// </summary>
    public bool SectionsAreFractions { get; }

    /// <summary>
    /// Gets the position of the field pointing at the bone name hashes.
    /// </summary>
    public long BoneHashesField { get; }

    /// <summary>
    /// Gets the offset, relative to the clip, of the curve name pointers, which is zero when names were not stored.
    /// </summary>
    public long CurveNamesOffset { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipHeader"/> class by reading the header at the given position.
    /// </summary>
    /// <param name="reader">The reader to read the header from.</param>
    /// <param name="offset">The position of the clip.</param>
    /// <param name="version">The .binanimclip version of the file.</param>
    /// <exception cref="NotSupportedException">Thrown when the version is not supported.</exception>
    public ClipHeader(BinaryReader reader, long offset, int version)
    {
        if (version is < MinimumVersion or > MaximumVersion)
            throw new NotSupportedException($"Clip version {version}");

        var isCompact = version >= CompactHeaderVersion;
        var hasSectionTimes = version >= SectionTimesVersion;
        var tailOffset = offset + (isCompact ? 0 : 32);

        Offset = offset;
        FrameCount = reader.ReadInt32(offset);
        BoneCount = reader.ReadUInt16(offset + 4);
        CurveCount = reader.ReadUInt16(offset + 6);
        CompressedCurveCount = isCompact ? reader.ReadByte(offset + 12) : reader.ReadUInt16(offset + 8);
        InstantEventCount = reader.ReadUInt16(offset + (isCompact ? 8 : 10));
        RangeEventCount = reader.ReadUInt16(offset + (isCompact ? 10 : 12));
        Compression = isCompact ? reader.ReadByte(offset + 13) : reader.ReadUInt16(offset + 14);
        VisibilityGroupCount = reader.ReadInt32(offset + 16);
        Duration = reader.ReadStruct<float>(offset + 20);
        Framerate = reader.ReadStruct<float>(offset + 24);

        BoneTracksField = offset + 32;
        TrajectoryField = offset + 36;
        CurveHashesField = offset + (isCompact ? 44 : 48);
        CurveIndicesField = offset + 52;
        CurveIndexSize = isCompact ? 1 : 2;
        CurvesField = offset + 56;
        InstantEventsField = offset + 60;
        RangeEventsField = offset + 64;

        VisibilityBitsField = tailOffset + 72;
        VisibilityBonesField = tailOffset + 76;
        VisibilityBoneCountsField = tailOffset + 80;

        SectionCount = hasSectionTimes ? reader.ReadInt32(offset + 92) : reader.ReadUInt16(tailOffset + 88);
        SectionsField = hasSectionTimes ? offset + 96 : tailOffset + 92;
        SectionsAreFractions = !hasSectionTimes;
        BoneHashesField = hasSectionTimes ? offset + 100 : tailOffset + 96;
        CurveNamesOffset = reader.ReadInt64(tailOffset + 104);
    }
}
