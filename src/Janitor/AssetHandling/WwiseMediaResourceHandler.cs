using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise media (.wem) assets stored in Pack2 files, which are exported byte-for-byte without conversion.
/// </summary>
public class WwiseMediaResourceHandler : IAssetHandler
{
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

        return Task.FromResult(context.ExportConfiguration.Overwrite || !File.Exists(context.ResolveAssetPath(asset)));
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var outputPath = context.ResolveAssetPath(result.Asset);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllBytesAsync(outputPath, result.GetData<byte[]>(), cancellationToken);
    }
}
