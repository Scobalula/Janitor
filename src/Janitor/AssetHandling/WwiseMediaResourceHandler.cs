using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using RedFox.GameExtraction;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise media (.wem) assets stored in Pack2 files, which are exported byte-for-byte unless the
/// <see cref="ConvertAudioOption"/> export option is set, in which case supported codecs are exported as PCM wave files.
/// </summary>
public class WwiseMediaResourceHandler : IAssetHandler
{
    /// <summary>
    /// The name of the boolean export option that converts media to wave files, which is off when not set.
    /// </summary>
    public const string ConvertAudioOption = "ConvertAudio";

    /// <inheritdoc/>
    public bool CanHandle(Asset asset)
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
    public Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = file.ReadAllBytes(),
        });
    }

    /// <inheritdoc/>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        if (asset.DataSource is Pack2File { Size: 0 })
            return Task.FromResult(false);

        var exists = File.Exists(context.ResolveAssetPath(asset)) || File.Exists(context.ResolveAssetPath(asset, WwiseMediaConverter.OutputExtension));

        return Task.FromResult(context.ExportConfiguration.Overwrite || !exists);
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var media = result.GetData<byte[]>();
        var outputPath = context.ResolveAssetPath(result.Asset);

        if (context.ExportConfiguration.GetOption(ConvertAudioOption, true) && WwiseMediaConverter.TryConvert(media, out var wave))
        {
            media = wave;
            outputPath = context.ResolveAssetPath(result.Asset, WwiseMediaConverter.OutputExtension);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllBytesAsync(outputPath, media, cancellationToken);
    }
}
