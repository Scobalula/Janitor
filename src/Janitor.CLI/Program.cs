

using Avalonia.Data;
using Janitor.Factory;
using RedFox.GameExtraction;
using RedFox.GameExtraction.Hashing;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Formats.SEAnim;
using RedFox.Graphics3D.Formats.SEModel;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO;
using RedFox.IO.FileSystem;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

var exportConfig = new ExportConfiguration()
{
    OutputDirectory = "exported_files"
};

var imageFormats = new string[] { ".dds" };
var modelFormats = new string[] { ".semodel", ".cast", ".fbx" };

exportConfig.SetOption("RelativeImages", false);
exportConfig.SetOption("SkipExistingImages", true);
exportConfig.SetOption("ModelFormats", modelFormats);
exportConfig.SetOption("ImageFormats", imageFormats);

var assetManager = JanitorAssetManagerFactory.Create();

assetManager.SourceMounted += (sender, e) =>
{
    Console.WriteLine($"Mounted source: {e.Source.Name} of type {e.Source.GetType()}");
};

assetManager.AssetReadStarting += (sender, e) =>
{
    Console.WriteLine($"Starting to read asset: {e.Asset.Name}");
};

assetManager.AssetReadCompleted += (sender, e) =>
{
    Console.WriteLine($"Completed reading asset: {e.Asset.Name}");
};

assetManager.AssetExportStarting += (sender, e) =>
{
    Console.WriteLine($"Starting to export asset: {e.Asset.Name}");
};

assetManager.AssetExportCompleted += (sender, e) =>
{
    Console.WriteLine($"Completed exporting asset: {e.Asset.Name}");
};

assetManager.OperationFailed += (sender, e) =>
{
    Console.WriteLine($"Operation failed for asset: {e.Asset?.Name}. Exception: {e.Exception}");
};

foreach (var arg in args)
{
    if (arg.EndsWith(".rmdtoc", StringComparison.OrdinalIgnoreCase))
    {
        await assetManager.MountFileAsync(arg);
    }
}

var vfs = assetManager.GetRequiredService<AssetFileSystemService>().FileSystem;

//#if DEBUG
//var nameTableManager = assetManager.GetRequiredService<NameListService>().Manager;

//if (!nameTableManager.TryGetTable("BoneTable", out var boneTable))
//{
//    boneTable = nameTableManager.CreateNameTable("BoneTable");
//}

//// Build Skeleton Hash Table
//foreach (var file in vfs.EnumerateFiles(null, "*.binskeleton", SearchOption.AllDirectories))
//{
//    if (file.Data is not Asset asset)
//        continue;

//    var data = await assetManager.ReadAsync(asset);
//    var node = data.GetData<SceneNode>();

//    foreach (var child in node.EnumerateDescendants<SkeletonBone>())
//    {
//        boneTable.Add((ulong)child.UserId, child.Name);
//    }
//}

//Directory.CreateDirectory("NameTables");
//NameFile.Save("NameTables\\BoneTable.namefile", boneTable, NameFileFlags.Checksum);
//#endif

// For now we just export .binfbx files to test the system, but eventually we will want to export all supported assets
foreach (var file in vfs.EnumerateFiles(null, "*cash*register*.binfbx", SearchOption.AllDirectories))
{
    if (file.Data is not Asset asset)
        continue;

    Console.WriteLine(asset.Name);
#if DEBUG
    file.CreateDirectory();
    file.WriteAllBytes();
#endif

    try
    {
        await assetManager.ExportAsync(asset, exportConfig);
    }
    catch
    {

    }
}