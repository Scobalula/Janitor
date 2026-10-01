using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Janitor.Wwise;

/// <summary>
/// Converts Wwise media (.wem) files to standard PCM wave files.
/// </summary>
public static class WwiseMediaConverter
{
    private const ushort VorbisCodec = 0xFFFF;
    private const ushort OpusCodec = 0x3041;
    private const ushort PcmCodec = 0xFFFE;
    private const ushort WavePcmFormat = 1;
    private const ushort WaveExtensibleFormat = 0xFFFE;
    private const int PcmBitsOffset = 14;
    private const int BitsPerSample = 16;
    private const int MaxPlainChannels = 2;
    private const int ExtensionSize = 22;

    private static readonly byte[] PcmSubFormat = [0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>
    /// Gets the extension of converted media.
    /// </summary>
    public static string OutputExtension => ".wav";

    /// <summary>
    /// Attempts to convert Wwise media to a wave file.
    /// </summary>
    /// <param name="media">The bytes of the .wem file.</param>
    /// <param name="wave">The bytes of the wave file when the codec is supported.</param>
    /// <returns><see langword="true"/> when the media was converted; otherwise, <see langword="false"/>, as is the case for plug-in media that holds no audio and codecs that are not supported.</returns>
    public static bool TryConvert(ReadOnlyMemory<byte> media, [NotNullWhen(true)] out byte[]? wave)
    {
        wave = null;

        if (!WwiseWaveFile.TryRead(media, out var waveFile))
            return false;

        wave = waveFile.Codec switch
        {
            VorbisCodec => CreateWave(waveFile.Channels, waveFile.SampleRate, BitsPerSample, WwiseVorbisDecoder.Decode(waveFile)),
            OpusCodec => CreateWave(waveFile.Channels, waveFile.SampleRate, BitsPerSample, WwiseOpusDecoder.Decode(waveFile)),
            PcmCodec => CreateWave(waveFile.Channels, waveFile.SampleRate, BinaryPrimitives.ReadUInt16LittleEndian(waveFile.Format.Span[PcmBitsOffset..]), waveFile.Data.Span),
            _ => null,
        };

        return wave is not null;
    }

    private static byte[] CreateWave(int channels, int sampleRate, int bitsPerSample, ReadOnlySpan<byte> pcm)
    {
        var extensible = channels > MaxPlainChannels;
        var formatSize = extensible ? 40 : 16;
        var blockAlign = channels * bitsPerSample / 8;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(4 + 8 + formatSize + 8 + pcm.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(formatSize);
        writer.Write(extensible ? WaveExtensibleFormat : WavePcmFormat);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)bitsPerSample);

        if (extensible)
        {
            writer.Write((ushort)ExtensionSize);
            writer.Write((ushort)bitsPerSample);
            writer.Write(0);
            writer.Write(PcmSubFormat);
        }

        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);

        return stream.ToArray();
    }
}
