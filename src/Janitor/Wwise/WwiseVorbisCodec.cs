using RedFox.Audio;

namespace Janitor.Wwise;

/// <summary>
/// The Wwise Vorbis codec, which stores standard Vorbis audio packets stripped of the fields a fixed decoder already knows.
/// The audio's setup data is the payload of the media's format chunk.
/// </summary>
public sealed class WwiseVorbisCodec : AudioCodec
{
    /// <inheritdoc/>
    public override string Id => "wwise-vorbis";

    /// <inheritdoc/>
    public override string Name => "Wwise Vorbis";

    /// <inheritdoc/>
    public override bool CanDecode => true;

    /// <inheritdoc/>
    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return new WwiseVorbisDecoder(audio);
    }
}
