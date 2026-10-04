using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using RedFox.Audio;
using RedFox.Audio.IO;
using RedFox.Audio.IO.Wav;
using RedFox.Audio.Opus;

namespace Janitor.Wwise;

/// <summary>
/// Reads Wwise media (.wem) files as audio clips.
/// PCM media is exposed as samples directly, while Vorbis and Opus media stay encoded until samples are requested.
/// </summary>
public sealed class WwiseMediaTranslator : AudioTranslator
{
    private const ushort VorbisCodecTag = 0xFFFF;
    private const ushort OpusCodecTag = 0x3041;
    private const ushort PcmCodecTag = 0xFFFE;
    private const int ChannelConfigOffset = 20;
    private const int ChannelConfigMaskShift = 12;
    private const int TotalFramesOffset = 24;
    private const int OpusPacketCountOffset = 28;
    private const int OpusPreSkipOffset = 32;
    private const int OpusMappingFamilyOffset = 35;
    private const int VorbisPacketsOffset = 44;

    private static readonly string[] SupportedExtensions = [".wem"];

    private readonly WavAudioTranslator _wav = new();
    private readonly OpusCodec _opus = new();
    private readonly WwiseVorbisCodec _vorbis = new();

    /// <inheritdoc/>
    public override string Name => "Wwise";

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Extensions => SupportedExtensions;

    /// <inheritdoc/>
    public override AudioClip Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        if (!TryRead(buffer.ToArray(), out AudioClip? clip))
            throw new AudioException("The data is not Wwise media with a supported codec.");

        return clip;
    }

    /// <summary>
    /// Attempts to read Wwise media as an audio clip without copying its sample or packet data.
    /// </summary>
    /// <param name="media">The bytes of the .wem file.</param>
    /// <param name="clip">The audio clip when the media is a wave file with a supported codec.</param>
    /// <returns><see langword="true"/> when the media was read; otherwise, <see langword="false"/>, as is the case for plug-in media that holds no audio and codecs that are not supported.</returns>
    public bool TryRead(Memory<byte> media, [NotNullWhen(true)] out AudioClip? clip)
    {
        clip = null;

        if (!WwiseWaveFile.TryRead(media, out WwiseWaveFile? waveFile))
            return false;

        clip = waveFile.Codec switch
        {
            PcmCodecTag => CreatePcm(media, waveFile),
            OpusCodecTag => new AudioClip(CreateOpus(waveFile)),
            VorbisCodecTag => new AudioClip(CreateVorbis(waveFile)),
            _ => null,
        };

        return clip is not null;
    }

    /// <inheritdoc/>
    public override void Write(Stream stream, AudioClip clip, AudioTranslatorOptions options) => throw new NotSupportedException("Wwise media cannot be written.");

    /// <inheritdoc/>
    public override bool IsValid(ReadOnlySpan<byte> header, string filePath, string extension) => IsValid(filePath, extension) && header.StartsWith("RIFF"u8);

    private static AudioFormat GetFormat(WwiseWaveFile waveFile)
    {
        uint config = BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[ChannelConfigOffset..]);
        uint mask = (config & 0xFF) == waveFile.Channels ? config >> ChannelConfigMaskShift : config;

        return BitOperations.PopCount(mask) == waveFile.Channels ? new AudioFormat(waveFile.SampleRate, waveFile.Channels, (ChannelLayout)mask) : new AudioFormat(waveFile.SampleRate, waveFile.Channels);
    }

    private AudioClip CreatePcm(Memory<byte> media, WwiseWaveFile waveFile)
    {
        AudioBuffer buffer = _wav.Read(media).GetBuffer();
        return new AudioClip(new AudioBuffer(GetFormat(waveFile), buffer.SampleFormat, buffer.Data, buffer.ValidBitsPerSample));
    }

    private EncodedAudio CreateOpus(WwiseWaveFile waveFile)
    {
        ReadOnlySpan<byte> format = waveFile.Format.Span;
        ReadOnlySpan<byte> seek = waveFile.Seek.Span;
        int packetCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[OpusPacketCountOffset..]);
        AudioPacket[] packets = new AudioPacket[packetCount];
        int offset = 0;

        if (seek.Length < packetCount * sizeof(ushort))
            throw new InvalidDataException("The seek table of a Wwise Opus file is shorter than its packet count.");

        for (int i = 0; i < packetCount; i++)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(seek[(i * sizeof(ushort))..]);

            if (offset + length > waveFile.Data.Length)
                throw new InvalidDataException("A Wwise Opus packet extends past the end of the data chunk.");

            packets[i] = new AudioPacket(offset, length);
            offset += length;
        }

        return new EncodedAudio
        {
            Codec = _opus,
            Format = GetFormat(waveFile),
            Data = waveFile.Data,
            FrameCount = BinaryPrimitives.ReadUInt32LittleEndian(format[TotalFramesOffset..]),
            Setup = OpusHeader.Create(waveFile.Channels, BinaryPrimitives.ReadUInt16LittleEndian(format[OpusPreSkipOffset..]), format[OpusMappingFamilyOffset]).ToBytes(),
            Packets = packets,
        };
    }

    private EncodedAudio CreateVorbis(WwiseWaveFile waveFile)
    {
        ReadOnlySpan<byte> data = waveFile.Data.Span;
        List<AudioPacket> packets = [];
        int position = (int)BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[VorbisPacketsOffset..]);

        while (position + sizeof(ushort) <= data.Length)
        {
            int length = BinaryPrimitives.ReadUInt16LittleEndian(data[position..]);
            position += sizeof(ushort);

            if (length == 0)
                break;

            if (position + length > data.Length)
                throw new InvalidDataException("A Wwise Vorbis packet extends past the end of the data chunk.");

            packets.Add(new AudioPacket(position, length));
            position += length;
        }

        return new EncodedAudio
        {
            Codec = _vorbis,
            Format = GetFormat(waveFile),
            Data = waveFile.Data,
            FrameCount = BinaryPrimitives.ReadUInt32LittleEndian(waveFile.Format.Span[TotalFramesOffset..]),
            Setup = waveFile.Format,
            Packets = packets.ToArray(),
        };
    }
}
