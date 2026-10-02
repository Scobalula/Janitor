using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using RedFox.GameExtraction;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles Wwise sound bank (.bnk) assets stored in Pack2 files, exporting every embedded media file as an individual .wem file
/// into a folder named after the bank. Banks that only hold objects and no embedded media export nothing.
/// </summary>
public class WwiseBankResourceHandler : IAssetHandler
{
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

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = WwiseBank.Read(file.ReadAllBytes()),
        });
    }

    /// <inheritdoc/>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        var bank = result.GetData<WwiseBank>();

        if (bank.Media.Count == 0)
            return;

        var bankDirectory = Path.Combine(context.ResolveAssetDirectory(result.Asset), Path.GetFileNameWithoutExtension(result.Asset.Name));

        Directory.CreateDirectory(bankDirectory);

        foreach (var media in bank.Media)
        {
            var outputPath = Path.Combine(bankDirectory, $"{media.Id}.wem");

            if (File.Exists(outputPath) && !context.Configuration.GetOption("Overwrite", false))
                continue;

            await File.WriteAllBytesAsync(outputPath, media.Data, cancellationToken);
        }
    }
}
