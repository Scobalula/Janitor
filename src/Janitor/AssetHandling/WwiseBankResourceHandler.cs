using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using RedFox.Audio;
using RedFox.Audio.IO;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise sound bank (.bnk) assets stored in Pack2 files. The embedded media with a supported codec is read as a list of
/// <see cref="AudioClip"/> instances, which are exported to the configured audio formats into a folder named after the bank.
/// </summary>
public class WwiseBankResourceHandler : IAssetHandler
{
    private readonly WwiseMediaTranslator _media = new();

    /// <inheritdoc/>
    public bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        WwiseBank bank = WwiseBank.Read(file.ReadAllBytes(cancellationToken));
        NameListService names = context.GetRequiredService<NameListService>();
        List<AudioClip> clips = [];

        foreach (WwiseEmbeddedMedia media in bank.Media)
        {
            if (!_media.TryRead(media.Data, out AudioClip? clip))
                continue;

            clip.Name = names.Manager.TryGetValue(WwiseMediaNameResolver.TableName, media.Id, out string? name) ? $"{name}_{media.Id}" : media.Id.ToString();
            clips.Add(clip);
        }

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = clips.ToArray(),
        });
    }

    /// <inheritdoc/>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        AudioClip[] clips = result.GetData<AudioClip[]>();
        AudioTranslatorManager manager = context.AssetManager.GetRequiredService<AudioTranslatorService>().Manager;
        string[] formats = context.Configuration.GetOption("AudioFormats", AudioClipHandler.DefaultFormats);
        bool skipExisting = context.Configuration.GetOption("SkipExistingAudio", true);
        string bankDirectory = Path.Combine(context.ResolveAssetDirectory(result.Asset), Path.GetFileNameWithoutExtension(result.Asset.Name));

        foreach (AudioClip clip in clips)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AudioClipHandler.ExportClip(clip, formats, manager, Path.Combine(bankDirectory, $"{clip.Name}.wem"), skipExisting, WwiseMediaResourceHandler.CreateExportOptions(clip));
        }

        return Task.CompletedTask;
    }
}
