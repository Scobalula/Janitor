using Janitor.AssetHandling;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;

namespace Janitor.Factory;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for Northlight Engine sources.
/// </summary>
public static class JanitorAssetManagerFactory
{
    /// <summary>
    /// The directory name tables are loaded from and saved to.
    /// </summary>
    public const string NameTablesDirectory = "NameTables";

    /// <summary>
    /// Creates an <see cref="AssetManager"/> configured with Pack2 mounting, the Northlight asset handlers,
    /// and the name tables found in <see cref="NameTablesDirectory"/>.
    /// </summary>
    /// <returns>A configured asset manager.</returns>
    public static AssetManager Create()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService(manager));

        manager.RegisterSourceReader(new Pack2SourceReader());
        manager.RegisterHandler(new MeshResourceHandler());
        manager.RegisterHandler(new MaterialResourceHandler());
        manager.RegisterHandler(new TextureResourceHandler());
        manager.RegisterHandler(new SkeletonResourceHandler());
        manager.RegisterHandler(new ClipResourceHandler());
        manager.RegisterHandler(new WwiseMediaResourceHandler());
        manager.RegisterHandler(new WwiseBankResourceHandler());
        manager.RegisterHandler(new RawAssetHandler());

        manager.RegisterService<ImageTranslatorService>();
        manager.RegisterService<SceneTranslatorService>();
        manager.RegisterService<ResourceTableService>();
        manager.RegisterService(NameListService.CreateFromDirectory(NameTablesDirectory));

        return manager;
    }
}
