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
    /// <summary>
    /// The name of the boolean export option that converts media to the configured audio formats, which is on when not set.
    /// </summary>
    public const string ConvertAudioOption = "ConvertAudio";

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
    public override Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (asset.DataSource is Pack2File { Size: 0 })
            return Task.FromResult(false);

        if (context.Configuration.GetOption(ConvertAudioOption, true))
            return base.ShouldExportAsync(asset, context, cancellationToken);

        return Task.FromResult(!context.Configuration.GetOption("SkipExistingAudio", true) || !File.Exists(context.ResolveAssetPath(asset)));
    }

    /// <inheritdoc/>
    public override async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (context.Configuration.GetOption(ConvertAudioOption, true) && result.Data is AudioClip clip)
        {
            AudioTranslatorManager manager = context.AssetManager.GetRequiredService<AudioTranslatorService>().Manager;
            string[] formats = context.Configuration.GetOption("AudioFormats", DefaultFormats);
            bool skipExisting = context.Configuration.GetOption("SkipExistingAudio", true);

            ExportClip(clip, formats, manager, context.ResolveAssetPath(result.Asset), skipExisting, CreateExportOptions(clip));
            return;
        }

        if (result.Asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        string outputPath = context.ResolveAssetPath(result.Asset);
        byte[] media = result.Data as byte[] ?? file.ReadAllBytes(cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllBytesAsync(outputPath, media, cancellationToken);
    }

    /// <summary>
    /// Creates the translator options used when converting Wwise media, which writes decoded Vorbis and Opus audio as 16-bit samples
    /// and keeps PCM media at its stored sample format.
    /// </summary>
    /// <param name="clip">The clip being converted.</param>
    /// <returns>The translator options for the clip.</returns>
    public static AudioTranslatorOptions CreateExportOptions(AudioClip clip) => new() { SampleFormat = clip.Encoded is null ? null : SampleFormat.Int16 };
}
