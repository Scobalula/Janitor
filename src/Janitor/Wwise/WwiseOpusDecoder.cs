using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Janitor.Wwise;

/// <summary>
/// Decodes Wwise Opus media to 16-bit PCM with libopus.
/// Wwise stores plain Opus packets with their sizes in a separate seek table.
/// Multichannel media is a multistream Opus stream, laid out the way libopus lays out the mapping family recorded in the media.
/// </summary>
public static unsafe class WwiseOpusDecoder
{
    private const int MaxFrameSize = 5760;
    private const int SurroundMappingFamily = 1;
    private const int DiscreteMappingFamily = 255;
    private const int MaxStereoChannels = 2;

    private static readonly (byte Streams, byte CoupledStreams, byte[] Mapping)[] SurroundLayouts =
    [
        (1, 0, [0]),
        (1, 1, [0, 1]),
        (2, 1, [0, 2, 1]),
        (2, 2, [0, 1, 2, 3]),
        (3, 2, [0, 4, 1, 2, 3]),
        (4, 2, [0, 4, 1, 2, 3, 5]),
        (5, 2, [0, 4, 1, 2, 3, 5, 6]),
        (5, 3, [0, 6, 1, 2, 3, 4, 5, 7]),
    ];

    /// <summary>
    /// Decodes Wwise Opus media.
    /// </summary>
    /// <param name="waveFile">The chunks of the Wwise Opus media.</param>
    /// <returns>The interleaved 16-bit little endian PCM samples, without the codec delay and trimmed to the length the media declares.</returns>
    /// <exception cref="InvalidDataException">Thrown when the media is truncated or malformed, or libopus rejects it.</exception>
    public static byte[] Decode(WwiseWaveFile waveFile)
    {
        var format = waveFile.Format.Span;
        var seek = waveFile.Seek.Span;
        var data = waveFile.Data.Span;
        var channels = waveFile.Channels;
        var totalFrames = BinaryPrimitives.ReadUInt32LittleEndian(format[24..]);
        var packetCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[28..]);
        var codecDelay = (int)BinaryPrimitives.ReadUInt16LittleEndian(format[32..]);
        var (streams, coupledStreams, mapping) = GetLayout(channels, format[35]);

        if (seek.Length < packetCount * sizeof(ushort))
            throw new InvalidDataException("The seek table of a Wwise Opus file is shorter than its packet count.");

        int error;

        nint decoder;

        fixed (byte* pointer = mapping)
            decoder = OpusInterop.CreateDecoder(waveFile.SampleRate, channels, streams, coupledStreams, pointer, &error);

        if (error != OpusInterop.Success)
            throw new InvalidDataException($"libopus failed to create a decoder: {error}.");

        try
        {
            using var pcm = new MemoryStream();

            var samples = new short[MaxFrameSize * channels];
            var position = 0;

            long framesWritten = 0;

            for (var i = 0; i < packetCount && framesWritten < totalFrames; i++)
            {
                var size = BinaryPrimitives.ReadUInt16LittleEndian(seek[(i * sizeof(ushort))..]);

                if (position + size > data.Length)
                    throw new InvalidDataException("A Wwise Opus packet extends past the end of the data chunk.");

                int decoded;

                fixed (byte* packet = data.Slice(position, size))
                fixed (short* output = samples)
                    decoded = OpusInterop.Decode(decoder, packet, size, output, MaxFrameSize, 0);

                if (decoded < 0)
                    throw new InvalidDataException($"libopus failed to decode packet {i}: {decoded}.");

                var skipped = Math.Min(codecDelay, decoded);
                var frames = (int)Math.Min(decoded - skipped, totalFrames - framesWritten);

                pcm.Write(MemoryMarshal.AsBytes(samples.AsSpan(skipped * channels, frames * channels)));

                codecDelay -= skipped;
                framesWritten += frames;
                position += size;
            }

            return pcm.ToArray();
        }
        finally
        {
            OpusInterop.DestroyDecoder(decoder);
        }
    }

    private static (byte Streams, byte CoupledStreams, byte[] Mapping) GetLayout(int channels, int mappingFamily)
    {
        if (mappingFamily == 0 && channels <= MaxStereoChannels || mappingFamily == SurroundMappingFamily && channels <= SurroundLayouts.Length)
            return SurroundLayouts[channels - 1];

        if (mappingFamily == DiscreteMappingFamily)
            return ((byte)channels, 0, [.. Enumerable.Range(0, channels).Select(x => (byte)x)]);

        throw new InvalidDataException($"A Wwise Opus file with mapping family {mappingFamily} and {channels} channels is not supported.");
    }
}
