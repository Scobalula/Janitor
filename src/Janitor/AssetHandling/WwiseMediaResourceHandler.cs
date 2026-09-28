using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using RedFox.GameExtraction;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise media (.wem) assets stored in Pack2 files, which are exported byte-for-byte without conversion.
/// Media that is only known by its identifier is prefixed with the name of the event that plays it when the
/// <see cref="WwiseMediaNameResolver.TableName"/> name table is available.
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

        return Task.FromResult(context.ExportConfiguration.Overwrite || !File.Exists(ResolveOutputPath(asset, context)));
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var outputPath = ResolveOutputPath(result.Asset, context);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllBytesAsync(outputPath, result.GetData<byte[]>(), cancellationToken);
    }

    private static string ResolveOutputPath(Asset asset, AssetExportContext context)
    {
        var outputPath = context.ResolveAssetPath(asset);

        if (!uint.TryParse(Path.GetFileNameWithoutExtension(asset.Name), out var mediaId))
            return outputPath;
        if (!context.GetRequiredService<NameListService>().Manager.TryGetValue(WwiseMediaNameResolver.TableName, mediaId, out var name))
            return outputPath;

        return Path.Combine(Path.GetDirectoryName(outputPath)!, $"{name}_{mediaId}.wem");
    }
}
