using Janitor.AssetHandling;
using Janitor.Pack2FileSystem;
using RedFox.GameExtraction;

namespace Janitor.Factory;

/// <summary>
/// Creates preconfigured <see cref="AssetManager"/> instances for ZIP-backed template applications.
/// </summary>
public static class JanitorAssetManagerFactory
{
    /// <summary>
    /// Creates an <see cref="AssetManager"/> configured with ZIP mounting, raw export support,
    /// and a shared virtual file system service.
    /// </summary>
    /// <returns>A configured asset manager.</returns>
    public static AssetManager Create()
    {
        AssetManager manager = new();
        manager.RegisterService(new AssetFileSystemService(manager));

        manager.AssetReadStarting += Manager_AssetReadStarting;
        manager.AssetReadCompleted += Manager_AssetReadCompleted;

        manager.RegisterSourceReader(new Pack2SourceReader());
        manager.RegisterHandler(new RawAssetHandler());
        manager.RegisterHandler(new MeshResourceHandler());
        manager.RegisterHandler(new MaterialResourceHandler());
        manager.RegisterHandler(new TextureResourceHandler());
        manager.RegisterHandler(new SkeletonResourceHandler());

        manager.RegisterService<ImageTranslatorService>();
        manager.RegisterService<SceneTranslatorService>();
        manager.RegisterService<ResourceTableService>();

        return manager;
    }

    private static void Manager_AssetReadCompleted(object? sender, AssetReadCompletedEventArgs e)
    {
        Console.WriteLine($"Asset read completed for asset: {Path.GetFileName(e.Asset.Name)}");
    }

    private static void Manager_AssetReadStarting(object? sender, AssetReadEventArgs e)
    {
        Console.WriteLine($"Beginning asset read for asset: {Path.GetFileName(e.Asset.Name)}");
    }
}