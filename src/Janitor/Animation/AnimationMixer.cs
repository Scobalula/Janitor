using RedFox.IO;

namespace Janitor.Animation;

/// <summary>
/// Reads the animation graphs an animation mixer (.binanimmixer) layers to animate a character.
/// </summary>
public sealed class AnimationMixer
{
    /// <summary>
    /// The oldest .binanimmixer version supported.
    /// </summary>
    public const int MinimumVersion = 12;

    /// <summary>
    /// The newest .binanimmixer version supported.
    /// </summary>
    public const int MaximumVersion = 15;

    /// <summary>
    /// The version that narrowed the graph count to a byte at the start of the mixer and moved the graph list behind it.
    /// </summary>
    public const int ByteCountVersion = 14;

    /// <summary>
    /// The version that moved the graph list 12 bytes further in.
    /// </summary>
    public const int ExtendedHeaderVersion = 15;

    private const int RelocationInfoOffset = 16;

    /// <summary>
    /// Gets the resource IDs of the graphs the mixer layers, in order.
    /// </summary>
    public IReadOnlyList<ulong> GraphResourceIds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationMixer"/> class by reading the mixer definition.
    /// </summary>
    /// <param name="reader">The reader to read the mixer from, positioned anywhere.</param>
    /// <exception cref="NotSupportedException">Thrown when the mixer version is not supported.</exception>
    public AnimationMixer(BinaryReader reader)
    {
        var version = reader.ReadInt32(0);

        if (version is < MinimumVersion or > MaximumVersion)
            throw new NotSupportedException($"Animation mixer version {version}");

        var offset = RelocationInfoOffset + reader.ReadUInt32(RelocationInfoOffset);
        var graphCount = version >= ByteCountVersion ? reader.ReadByte(offset) : reader.ReadInt32(offset + 8);
        var graphsField = offset + (version >= ExtendedHeaderVersion ? 16 : version >= ByteCountVersion ? 4 : 0);

        GraphResourceIds = reader.ReadStructArray<ulong>(graphCount, graphsField + reader.ReadInt32(graphsField)).ToArray();
    }
}
