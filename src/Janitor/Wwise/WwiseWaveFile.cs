using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Janitor.Wwise;

/// <summary>
/// The chunks of a Wwise media (.wem) file that are needed to convert it, which is a RIFF wave file whose format tag identifies the Wwise codec.
/// </summary>
/// <param name="Format">The payload of the format chunk.</param>
/// <param name="Seek">The payload of the seek chunk, which is empty when the file has none.</param>
/// <param name="Data">The payload of the data chunk.</param>
public sealed class WwiseWaveFile(ReadOnlyMemory<byte> format, ReadOnlyMemory<byte> seek, ReadOnlyMemory<byte> data)
{
    private const uint RiffTag = 0x46464952;
    private const uint WaveTag = 0x45564157;
    private const uint FormatTag = 0x20746D66;
    private const uint DataTag = 0x61746164;
    private const uint SeekTag = 0x6B656573;
    private const int RiffHeaderSize = 12;
    private const int ChunkHeaderSize = 8;

    /// <summary>
    /// Gets the payload of the format chunk.
    /// </summary>
    public ReadOnlyMemory<byte> Format { get; } = format;

    /// <summary>
    /// Gets the payload of the seek chunk, which is empty when the file has none.
    /// </summary>
    public ReadOnlyMemory<byte> Seek { get; } = seek;

    /// <summary>
    /// Gets the payload of the data chunk.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; } = data;

    /// <summary>
    /// Gets the format tag that identifies the codec of the media.
    /// </summary>
    public ushort Codec => BinaryPrimitives.ReadUInt16LittleEndian(Format.Span);

    /// <summary>
    /// Gets the number of channels of the media.
    /// </summary>
    public int Channels => BinaryPrimitives.ReadUInt16LittleEndian(Format.Span[2..]);

    /// <summary>
    /// Gets the sample rate of the media in Hz.
    /// </summary>
    public int SampleRate => (int)BinaryPrimitives.ReadUInt32LittleEndian(Format.Span[4..]);

    /// <summary>
    /// Attempts to read the chunks of a Wwise media file.
    /// </summary>
    /// <param name="media">The bytes of the .wem file.</param>
    /// <param name="waveFile">The chunks of the file when it is a wave file with a format and a data chunk.</param>
    /// <returns><see langword="true"/> when the file is a wave file; otherwise, <see langword="false"/>, as is the case for plug-in media that holds no audio.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> media, [NotNullWhen(true)] out WwiseWaveFile? waveFile)
    {
        waveFile = null;

        var span = media.Span;

        if (span.Length < RiffHeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(span) != RiffTag || BinaryPrimitives.ReadUInt32LittleEndian(span[8..]) != WaveTag)
            return false;

        var format = ReadOnlyMemory<byte>.Empty;
        var seek = ReadOnlyMemory<byte>.Empty;
        var data = ReadOnlyMemory<byte>.Empty;
        var position = RiffHeaderSize;

        while (position + ChunkHeaderSize <= span.Length)
        {
            var tag = BinaryPrimitives.ReadUInt32LittleEndian(span[position..]);
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(span[(position + 4)..]), (uint)(span.Length - position - ChunkHeaderSize));
            var payload = media.Slice(position + ChunkHeaderSize, size);

            if (tag == FormatTag)
                format = payload;
            else if (tag == SeekTag)
                seek = payload;
            else if (tag == DataTag)
                data = payload;

            position += ChunkHeaderSize + size + (size & 1);
        }

        if (format.IsEmpty || data.IsEmpty)
            return false;

        waveFile = new WwiseWaveFile(format, seek, data);

        return true;
    }
}
