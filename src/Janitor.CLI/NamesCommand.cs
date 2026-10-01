using Janitor.Factory;
using Janitor.Wwise;
using RedFox.GameExtraction;
using RedFox.GameExtraction.CommandLine;
using RedFox.GameExtraction.Hashing;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.IO.FileSystem;
using Spectre.Console;

namespace Janitor.CLI;

internal sealed class NamesCommand : ICommandLineCommand
{
    private const string BoneTableName = "BoneTable";
    private const string BonesKeyword = "bones";
    private const string MediaKeyword = "media";
    private const string AllKeyword = "all";

    public string Name => "names";

    public string Usage => "[bones|media|all]";

    public string Description => "Build bone and audio name tables from mounted sources";

    public async Task ExecuteAsync(CommandLineSession session, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string target = arguments.Count == 0 ? AllKeyword : arguments[0].ToLowerInvariant();

        if (target is not (BonesKeyword or MediaKeyword or AllKeyword))
        {
            throw new ArgumentException($"Unknown name table {arguments[0]}");
        }

        if (session.Manager.Sources.Count == 0)
        {
            throw new InvalidOperationException("No sources mounted");
        }

        VirtualFileSystem fileSystem = session.Manager.GetRequiredService<AssetFileSystemService>().FileSystem;
        NameTableManager nameTables = session.Manager.GetRequiredService<NameListService>().Manager;
        NameTable? boneTable = null;
        NameTable? mediaTable = null;

        Directory.CreateDirectory(JanitorAssetManagerFactory.NameTablesDirectory);

        await session.CreateProgress().StartAsync(async context =>
        {
            if (target is BonesKeyword or AllKeyword)
            {
                boneTable = await BuildBoneTableAsync(session.Manager, fileSystem, nameTables, context, cancellationToken);
            }

            if (target is MediaKeyword or AllKeyword)
            {
                mediaTable = await BuildMediaTableAsync(session.Manager, fileSystem, nameTables, context, cancellationToken);
            }
        });

        if (boneTable is not null)
        {
            session.WriteSuccess($"{BoneTableName} [{session.Theme.Muted}]· {boneTable.Count:N0} names[/]");
        }

        if (mediaTable is not null)
        {
            session.WriteSuccess($"{WwiseMediaNameResolver.TableName} [{session.Theme.Muted}]· {mediaTable.Count:N0} names[/]");
            await session.RemountAsync(cancellationToken);
        }
    }

    public IEnumerable<string> GetCompletions(CommandLineSession session, IReadOnlyList<string> arguments) => [BonesKeyword, MediaKeyword, AllKeyword];

    private static async Task<NameTable> BuildBoneTableAsync(AssetManager manager, VirtualFileSystem fileSystem, NameTableManager nameTables, ProgressContext context, CancellationToken cancellationToken)
    {
        NameTable table = GetEmptyTable(nameTables, BoneTableName);
        List<Asset> skeletons = FindAssets(fileSystem, "*.binskeleton");
        ProgressTask task = context.AddTask("Bones", maxValue: skeletons.Count);

        foreach (Asset asset in skeletons)
        {
            AssetReadResult result = await manager.ReadAsync(asset, cancellationToken);

            foreach (SkeletonBone bone in result.GetData<SceneNode>().EnumerateDescendants<SkeletonBone>())
            {
                table.Add((ulong)bone.UserId, bone.Name);
            }

            task.Increment(1);
        }

        NameFile.Save(GetTablePath(BoneTableName), table, NameFileFlags.Checksum);

        return table;
    }

    private static async Task<NameTable> BuildMediaTableAsync(AssetManager manager, VirtualFileSystem fileSystem, NameTableManager nameTables, ProgressContext context, CancellationToken cancellationToken)
    {
        NameTable table = GetEmptyTable(nameTables, WwiseMediaNameResolver.TableName);
        WwiseMediaNameResolver resolver = new();
        List<Asset> banks = FindAssets(fileSystem, "*.bnk");
        ProgressTask task = context.AddTask("Audio", maxValue: banks.Count);

        foreach (Asset asset in banks)
        {
            AssetReadResult result = await manager.ReadAsync(asset, cancellationToken);
            resolver.AddBank(Path.GetFileNameWithoutExtension(asset.Name), result.GetData<WwiseBank>());
            task.Increment(1);
        }

        foreach ((uint mediaId, string mediaName) in resolver.Resolve())
        {
            table.Add(mediaId, mediaName);
        }

        NameFile.Save(GetTablePath(WwiseMediaNameResolver.TableName), table, NameFileFlags.Checksum);

        return table;
    }

    private static NameTable GetEmptyTable(NameTableManager nameTables, string name)
    {
        if (!nameTables.TryGetTable(name, out NameTable? table))
        {
            return nameTables.CreateNameTable(name);
        }

        table.Clear();

        return table;
    }

    private static List<Asset> FindAssets(VirtualFileSystem fileSystem, string pattern) => [.. fileSystem.EnumerateFiles(null, pattern, SearchOption.AllDirectories).Select(file => file.Data).OfType<Asset>()];

    private static string GetTablePath(string name) => Path.Combine(JanitorAssetManagerFactory.NameTablesDirectory, name + ".namefile");
}
