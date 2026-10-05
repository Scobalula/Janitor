using Janitor.Animation;
using Janitor.Pack2FileSystem;
using Microsoft.Extensions.Logging;
using RedFox.GameExtraction;
using RedFox.GameExtraction.AssetHandlers;
using RedFox.Graphics3D;

namespace Janitor.AssetHandling;

/// <summary>
/// Handles animation graph (.binanimgraph) and animation mixer (.binanimmixer) assets, reading every clip the graphs sample into its
/// own scene, with clips a graph layers additively made relative to the base pose it subtracts from them.
/// </summary>
public class GraphResourceHandler : IAssetHandler
{
    private const string MixerExtension = ".binanimmixer";

    /// <inheritdoc/>
    public bool CanHandle(Asset asset, GameExtractionConfiguration configuration)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".binanimgraph", StringComparison.OrdinalIgnoreCase) && !asset.Name.EndsWith(MixerExtension, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <inheritdoc/>
    public Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        // * Mixers layer several graphs, so their clips are gathered from every graph.
        // * A clip layered against different base poses uses the first one found.
        cancellationToken.ThrowIfCancellationRequested();

        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        var resourceTable = context.GetRequiredService<ResourceTableService>().Resources;
        var graphs = asset.Name.EndsWith(MixerExtension, StringComparison.OrdinalIgnoreCase) ? ReadMixerGraphs(file, resourceTable) : [ReadGraph(file)];
        var scenes = new List<Scene>();

        if (graphs.All(x => x.ClipResourceIds.Count == 0))
            throw new InvalidDataException($"'{asset.Name}' samples no clips, it only blends poses handed to it by other graphs.");

        context.TryGetService<NameListService>(out var nameList);

        var flipModelsAxis = AxisFlip.Parse(context.Configuration.GetOption("FlipModelsAxis", "None"));
        var modelScale = ModelScale.Read(context.Configuration);

        foreach (var clipResourceId in graphs.SelectMany(x => x.ClipResourceIds).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!resourceTable.TryGetValue(clipResourceId, out var clipFile))
                throw new KeyNotFoundException($"Clip resource with ID 0x{clipResourceId:X16} not found in resource table.");

            var animation = ClipResourceHandler.ReadAnimation(clipFile, nameList, graphs.SelectMany(x => x.AdditiveClips).FirstOrDefault(x => x.ClipResourceId == clipResourceId), flipModelsAxis, modelScale);
            var scene = new Scene(animation.Name);

            scene.AddNode(animation);
            scenes.Add(scene);
        }

        context.AssetManager.Logger.LogDebug("Read graph {Graph} with {SceneCount} clip scenes", asset.Name, scenes.Count);

        return Task.FromResult(new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = scenes.ToArray(),
        });
    }

    /// <inheritdoc/>
    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manager = context.GetRequiredService<SceneTranslatorService>().Manager;
        var formats = context.Configuration.GetOption("AnimationFormats", AnimationHandler.DefaultFormats);
        var outputDirectory = context.ResolveAssetPath(result.Asset);

        Directory.CreateDirectory(outputDirectory);

        foreach (var scene in result.GetData<Scene[]>())
        {
            foreach (var format in formats)
                await manager.WriteAsync(Path.Combine(outputDirectory, scene.Name + format), scene, new(), cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken) => Task.FromResult(true);

    private static AnimationGraph ReadGraph(Pack2File file)
    {
        using var reader = new BinaryReader(file.Open());

        return new AnimationGraph(reader);
    }

    private static AnimationGraph[] ReadMixerGraphs(Pack2File file, Dictionary<ulong, Pack2File> resourceTable)
    {
        using var reader = new BinaryReader(file.Open());

        var mixer = new AnimationMixer(reader);
        var graphs = new AnimationGraph[mixer.GraphResourceIds.Count];

        for (var i = 0; i < graphs.Length; i++)
        {
            if (!resourceTable.TryGetValue(mixer.GraphResourceIds[i], out var graphFile))
                throw new KeyNotFoundException($"Animation graph resource with ID 0x{mixer.GraphResourceIds[i]:X16} not found in resource table.");

            graphs[i] = ReadGraph(graphFile);
        }

        return graphs;
    }
}
