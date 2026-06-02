

using Janitor.Factory;
using RedFox.GameExtraction;
using RedFox.Graphics3D;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.Semodel;
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
    ExportReferences = true,
    OutputDirectory = "export_files"
};
var imageFormats = new string[] { ".semodel", ".smd", ".fbx" };
exportConfig.SetOption("RelativeImages", true);
exportConfig.SetOption("ModelFormats", imageFormats);

var assetManager = JanitorAssetManagerFactory.Create();

await assetManager.MountFileAsync(@"D:\AlanWake2\AlanWake2\data_pack2\pc\base-generic.rmdtoc");

foreach (var asset in assetManager.Assets)
{
    if (!asset.Name.EndsWith("20230525_sabi_perez_lw_station_state_2_group_1_lawyer_18326.wem", StringComparison.OrdinalIgnoreCase))
        continue;

    Console.WriteLine(asset.Name);

    if (asset.DataSource is VirtualFile file)
    {
        if (file.DirectoryPath is string directory)
            Directory.CreateDirectory(directory);

        file.WriteAllBytes();
    }

    //await assetManager.ExportAsync(asset, exportConfig);
}