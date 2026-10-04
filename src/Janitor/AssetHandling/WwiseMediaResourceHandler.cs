using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using RedFox.Audio;
using RedFox.Audio.IO;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise media (.wem) assets stored in Pack2 files. Media with a supported codec is read as an <see cref="AudioClip"/>,
/// which is exported to the configured audio formats through the <see cref="AudioTranslatorService"/> when the
/// <see cref="ConvertAudioOption"/> export option is set, and byte-for-byte otherwise.
/// </summary>
public class WwiseMediaResourceHandler : AudioClipHandler
{
    private readonly WwiseMediaTranslator _media = new();

    /// <inheritdoc/>
    public override bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".wem", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public override Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        byte[] media = file.ReadAllBytes(cancellationToken);

        if (!_media.TryRead(media, out AudioClip? clip))
            return Task.FromResult(new AssetReadResult { Asset = asset, Handler = this, Data = media });

        clip.Name = Path.GetFileNameWithoutExtension(asset.Name);

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = clip,
        });
    }

    /// <inheritdoc/>
    public override async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return !context.Configuration.GetOption("SkipExistingAudio", true) || !File.Exists(context.ResolveAssetPath(asset));
    }

    /// <inheritdoc/>
    public override async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (result.Data is not AudioClip clip)
            return;

        AudioTranslatorManager manager = context.AssetManager.GetRequiredService<AudioTranslatorService>().Manager;
        string[] formats = context.Configuration.GetOption("AudioFormats", DefaultFormats);
        bool skipExisting = context.Configuration.GetOption("SkipExistingAudio", true);

        ExportClip(clip, formats, manager, context.ResolveAssetPath(result.Asset), skipExisting, CreateExportOptions(clip));
    }

    /// <summary>
    /// Creates the translator options used when converting Wwise media, which writes decoded Vorbis and Opus audio as 16-bit samples
    /// and keeps PCM media at its stored sample format.
    /// </summary>
    /// <param name="clip">The clip being converted.</param>
    /// <returns>The translator options for the clip.</returns>
    public static AudioTranslatorOptions CreateExportOptions(AudioClip clip) => new() { SampleFormat = clip.Encoded is null ? null : SampleFormat.Int16 };
}
