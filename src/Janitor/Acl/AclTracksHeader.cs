using System.Buffers.Binary;

namespace Janitor.Acl;

/// <summary>
/// Describes the raw buffer and tracks headers found at the start of every ACL compressed tracks buffer.
/// </summary>
/// <param name="buffer">The compressed tracks buffer, starting at its raw buffer header.</param>
public readonly struct AclTracksHeader(ReadOnlySpan<byte> buffer)
{
    /// <summary>
    /// The serialization tag identifying a compressed tracks buffer.
    /// </summary>
    public const uint CompressedTracksTag = 0xAC11AC11;

    /// <summary>
    /// The size of the raw buffer and tracks headers combined, in bytes.
    /// </summary>
    public const int Size = 32;

    /// <summary>
    /// The oldest serialization version supported, ACL 2.1.0-wip, which only differs from ACL 2.1.0 in its database metadata.
    /// </summary>
    public const ushort MinimumSupportedVersion = 9;

    /// <summary>
    /// The newest serialization version supported, ACL 2.1.0.
    /// </summary>
    public const ushort MaximumSupportedVersion = 10;

    /// <summary>
    /// Gets the total size of the compressed tracks buffer in bytes.
    /// </summary>
    public uint BufferSize { get; } = BinaryPrimitives.ReadUInt32LittleEndian(buffer);

    /// <summary>
    /// Gets the serialization tag of the buffer.
    /// </summary>
    public uint Tag { get; } = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..]);

    /// <summary>
    /// Gets the serialization version the tracks were compressed with.
    /// </summary>
    public ushort Version { get; } = BinaryPrimitives.ReadUInt16LittleEndian(buffer[12..]);

    /// <summary>
    /// Gets the algorithm the tracks were compressed with, 0 being uniform sampling.
    /// </summary>
    public byte AlgorithmType { get; } = buffer[14];

    /// <summary>
    /// Gets the type of the tracks stored in the buffer.
    /// </summary>
    public AclTrackType TrackType { get; } = (AclTrackType)buffer[15];

    /// <summary>
    /// Gets the number of tracks stored in the buffer.
    /// </summary>
    public int TrackCount { get; } = BinaryPrimitives.ReadInt32LittleEndian(buffer[16..]);

    /// <summary>
    /// Gets the number of samples stored per track.
    /// </summary>
    public int SampleCount { get; } = BinaryPrimitives.ReadInt32LittleEndian(buffer[20..]);

    /// <summary>
    /// Gets the rate, in samples per second, the tracks were sampled at.
    /// </summary>
    public float SampleRate { get; } = BinaryPrimitives.ReadSingleLittleEndian(buffer[24..]);

    /// <summary>
    /// Gets the packed flags and formats of the tracks.
    /// </summary>
    public uint MiscPacked { get; } = BinaryPrimitives.ReadUInt32LittleEndian(buffer[28..]);

    /// <summary>
    /// Gets whether transform tracks store scale sub-tracks.
    /// </summary>
    public bool HasScale => (MiscPacked & 1) != 0;

    /// <summary>
    /// Gets the value of every component of a default scale sub-track, either 0 or 1.
    /// </summary>
    public float DefaultScale => (MiscPacked >> 1) & 1;

    /// <summary>
    /// Gets the format scale sub-tracks are stored in.
    /// </summary>
    public AclVectorFormat ScaleFormat => (AclVectorFormat)((MiscPacked >> 2) & 1);

    /// <summary>
    /// Gets the format translation sub-tracks are stored in.
    /// </summary>
    public AclVectorFormat TranslationFormat => (AclVectorFormat)((MiscPacked >> 3) & 1);

    /// <summary>
    /// Gets the format rotation sub-tracks are stored in.
    /// </summary>
    public AclRotationFormat RotationFormat => (AclRotationFormat)((MiscPacked >> 4) & 15);

    /// <summary>
    /// Gets whether part of the samples live in an external streaming database.
    /// </summary>
    public bool HasDatabase => (MiscPacked & (1 << 8)) != 0;

    /// <summary>
    /// Gets whether default sub-tracks use the identity transform, rather than the bind pose.
    /// </summary>
    public bool HasTrivialDefaultValues => (MiscPacked & (1 << 9)) != 0;

    /// <summary>
    /// Gets whether keyframes were stripped from the tracks.
    /// </summary>
    public bool HasStrippedKeyframes => (MiscPacked & (1 << 10)) != 0;

    /// <summary>
    /// Ensures the buffer holds uniformly sampled tracks of the given type, compressed with a supported version.
    /// </summary>
    /// <param name="trackType">The type of tracks the buffer is expected to hold.</param>
    /// <exception cref="InvalidDataException">Thrown when the buffer does not contain compressed tracks of the given type.</exception>
    /// <exception cref="NotSupportedException">Thrown when the tracks use an unsupported version or algorithm.</exception>
    public void EnsureSupported(AclTrackType trackType)
    {
        if (Tag != CompressedTracksTag || TrackType != trackType)
            throw new InvalidDataException($"Buffer does not contain ACL compressed {trackType} tracks.");
        if (Version is < MinimumSupportedVersion or > MaximumSupportedVersion)
            throw new NotSupportedException($"ACL compressed tracks version {Version}");
        if (AlgorithmType != 0)
            throw new NotSupportedException($"ACL algorithm type {AlgorithmType}");
    }
}
