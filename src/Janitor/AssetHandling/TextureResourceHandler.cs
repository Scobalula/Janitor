using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;
using RedFox.Imaging.IO;
using RedFox.Graphics3D;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

public class TextureResourceHandler : IAssetHandler
{
    public static void ExportMaterialImages(IEnumerable<Texture> textures, IEnumerable<string> formats, ImageTranslatorManager manager, string directory, bool stripImagePath, bool skipExisting)
    {
        foreach (var texture in textures)
        {
            ExportImage(texture, formats, manager, directory, stripImagePath, skipExisting);
        }
    }

    public static void ExportImage(Texture texture, IEnumerable<string> formats, ImageTranslatorManager manager, string directory, bool stripImagePath, bool skipExisting)
    {
        if (texture.ImageLoader is null)
            throw new NullReferenceException(nameof(texture.ImageLoader));

        var data = texture.ImageLoader.Load(texture, manager);
        var path = texture.Name;

        if (stripImagePath)
            path = Path.Combine(directory, Path.GetFileName(path));
        else
            path = Path.Combine(directory, path);

        if (Path.GetDirectoryName(path) is string directoryToCreate)
            Directory.CreateDirectory(directoryToCreate);

        foreach (var format in formats)
        {
            // We will essentially assign the last written image
            // as this textures new "path" for models, etc.
            texture.FilePath = Path.GetFullPath(Path.ChangeExtension(path, format));

            if (skipExisting && File.Exists(texture.FilePath))
                continue;

            manager.Write(texture.FilePath, data);
        }
    }

    public bool CanHandle(Asset asset)
    {
        if (asset.Source is not Pack2Source)
            return false;
        if (asset.DataSource is not Pack2File)
            return false;
        if (!asset.Name.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext context, CancellationToken cancellationToken)
    {
        if (asset.Source is not Pack2Source)
            throw new NotSupportedException("Only Pack2Source assets are supported.");
        if (asset.DataSource is not Pack2File file)
            throw new NotSupportedException("Only Pack2File data sources are supported.");

        // Simply return 
        return new AssetReadResult
        {
            Asset = asset,
            Handler = this,
            Data = new Texture(asset.Name)
            {
                ImageLoader = Pack2FileImageLoader.Shared,
                UserData = file
            }
        };
    }

    public async Task<bool> ShouldExportAsync(Asset asset, AssetExportContext context, CancellationToken cancellationToken)
    {
        return true;
    }

    public async Task ExportAsync(AssetReadResult result, AssetExportContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
