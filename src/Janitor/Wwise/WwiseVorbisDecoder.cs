using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Janitor.Wwise;

/// <summary>
/// Decodes Wwise Vorbis media to 16-bit PCM with libvorbis.
/// Wwise stores the same Vorbis audio packets as any other encoder, minus the fields a fixed decoder already knows,
/// so the three headers are rebuilt and the missing fields are restored on every packet before libvorbis decodes them.
/// </summary>
public static unsafe class WwiseVorbisDecoder
{
    private const int FirstAudioPacketNumber = 3;
    private const int HeaderCount = 3;
    private const int WwiseModeBits = 1;
    private const int MaxModes = 1 << WwiseModeBits;

    /// <summary>
    /// Decodes Wwise Vorbis media.
    /// </summary>
    /// <param name="waveFile">The chunks of the Wwise Vorbis media.</param>
    /// <returns>The interleaved 16-bit little endian PCM samples, trimmed to the length the media declares.</returns>
    /// <exception cref="InvalidDataException">Thrown when the media is truncated or malformed, or libvorbis rejects it.</exception>
    public static byte[] Decode(WwiseWaveFile waveFile)
    {
        var format = waveFile.Format.Span;
        var data = waveFile.Data.Span;
        var channels = waveFile.Channels;
        var totalFrames = BinaryPrimitives.ReadUInt32LittleEndian(format[24..]);
        var setupOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[40..]);
        var packetsOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[44..]);
        var setupSize = BinaryPrimitives.ReadUInt16LittleEndian(data[setupOffset..]);

        byte[][] headers =
        [
            VorbisHeaderBuilder.BuildIdentificationHeader(channels, waveFile.SampleRate, format[64], format[65]),
            VorbisHeaderBuilder.BuildCommentHeader(),
            VorbisHeaderBuilder.BuildSetupHeader(data.Slice(setupOffset + sizeof(ushort), setupSize), PackedCodebooks.Default, channels, out var modeBlockFlags),
        ];

        if (modeBlockFlags.Length > MaxModes)
            throw new InvalidDataException($"A Wwise Vorbis file has {modeBlockFlags.Length} modes, but a packet can only select between {MaxModes}.");

        var modeBits = 32 - BitOperations.LeadingZeroCount((uint)(modeBlockFlags.Length - 1));
        var packets = ReadPackets(data, packetsOffset);
        var longBlocks = new bool[packets.Count];

        for (var i = 0; i < packets.Count; i++)
        {
            var mode = data[packets[i].Start] & 1;

            if (mode >= modeBlockFlags.Length)
                throw new InvalidDataException($"Packet {i} of a Wwise Vorbis file selects mode {mode}, which the file does not define.");

            longBlocks[i] = modeBlockFlags[mode];
        }

        var info = NativeMemory.AllocZeroed(VorbisInterop.InfoSize);
        var comment = NativeMemory.AllocZeroed(VorbisInterop.CommentSize);
        var dspState = NativeMemory.AllocZeroed(VorbisInterop.DspStateSize);
        var block = NativeMemory.AllocZeroed(VorbisInterop.BlockSize);
        var synthesisStarted = false;

        try
        {
            VorbisInterop.InitializeInfo(info);
            VorbisInterop.InitializeComment(comment);

            for (var i = 0; i < HeaderCount; i++)
                ReadHeader(info, comment, headers[i], i);

            ThrowIfFailed(VorbisInterop.InitializeSynthesis(dspState, info), "start synthesis");
            ThrowIfFailed(VorbisInterop.InitializeBlock(dspState, block), "start a block");

            synthesisStarted = true;

            using var pcm = new MemoryStream();

            long framesWritten = 0;

            for (var i = 0; i < packets.Count && framesWritten < totalFrames; i++)
            {
                var restored = RestorePacket(data[packets[i]], modeBits, longBlocks, i);

                fixed (byte* pointer = restored)
                {
                    var packet = new OggPacket { Data = (nint)pointer, Length = restored.Length, GranulePosition = -1, PacketNumber = FirstAudioPacketNumber + i };

                    ThrowIfFailed(VorbisInterop.Synthesize(block, &packet), $"synthesize packet {i}");
                    ThrowIfFailed(VorbisInterop.BlockIn(dspState, block), $"process packet {i}");
                }

                framesWritten += WritePcm(dspState, channels, totalFrames - framesWritten, pcm);
            }

            return pcm.ToArray();
        }
        finally
        {
            if (synthesisStarted)
            {
                VorbisInterop.ClearBlock(block);
                VorbisInterop.ClearDspState(dspState);
            }

            VorbisInterop.ClearComment(comment);
            VorbisInterop.ClearInfo(info);

            NativeMemory.Free(info);
            NativeMemory.Free(comment);
            NativeMemory.Free(dspState);
            NativeMemory.Free(block);
        }
    }

    private static List<Range> ReadPackets(ReadOnlySpan<byte> data, int position)
    {
        var packets = new List<Range>();

        while (position + sizeof(ushort) <= data.Length)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(data[position..]);

            position += sizeof(ushort);

            if (size == 0)
                break;

            if (position + size > data.Length)
                throw new InvalidDataException("A Wwise Vorbis packet extends past the end of the data chunk.");

            packets.Add(position..(position + size));

            position += size;
        }

        return packets;
    }

    private static byte[] RestorePacket(ReadOnlySpan<byte> packet, int modeBits, bool[] longBlocks, int index)
    {
        var writer = new OggBitWriter(packet.Length + 4);

        writer.WriteBits(0, 1);
        writer.WriteBits((uint)(packet[0] & 1), modeBits);

        if (longBlocks[index])
        {
            writer.WriteBits(index > 0 && longBlocks[index - 1] ? 1u : 0u, 1);
            writer.WriteBits(index < longBlocks.Length - 1 && longBlocks[index + 1] ? 1u : 0u, 1);
        }

        writer.CopyBits(packet, WwiseModeBits, packet.Length * 8 - WwiseModeBits);

        return writer.ToArray();
    }

    private static void ReadHeader(void* info, void* comment, byte[] header, int number)
    {
        fixed (byte* pointer = header)
        {
            var packet = new OggPacket { Data = (nint)pointer, Length = header.Length, BeginningOfStream = number == 0 ? 1 : 0, PacketNumber = number };

            ThrowIfFailed(VorbisInterop.ReadHeader(info, comment, &packet), $"read header {number}");
        }
    }

    private static long WritePcm(void* dspState, int channels, long framesLeft, MemoryStream output)
    {
        long framesWritten = 0;

        float** pcm;

        int available;

        while ((available = VorbisInterop.GetPcm(dspState, &pcm)) > 0)
        {
            var frames = (int)Math.Min(available, framesLeft - framesWritten);
            var samples = new byte[frames * channels * sizeof(short)];

            for (var frame = 0; frame < frames; frame++)
            {
                for (var channel = 0; channel < channels; channel++)
                    BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan((frame * channels + channel) * sizeof(short)), (short)(Math.Clamp(pcm[channel][frame], -1.0f, 1.0f) * 32767.0f));
            }

            output.Write(samples);

            VorbisInterop.ReadPcm(dspState, available);

            framesWritten += frames;
        }

        return framesWritten;
    }

    private static void ThrowIfFailed(int result, string operation)
    {
        if (result != 0)
            throw new InvalidDataException($"libvorbis failed to {operation}: {result}.");
    }
}
