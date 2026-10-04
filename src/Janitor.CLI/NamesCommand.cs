using Janitor.Factory;
using Janitor.Pack2FileSystem;
using Janitor.Wwise;
using Microsoft.Extensions.Logging;
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
        ILogger logger = session.Manager.Logger;
        NameTable? boneTable = null;
        NameTable? mediaTable = null;

        Directory.CreateDirectory(JanitorAssetManagerFactory.NameTablesDirectory);

        logger.LogInformation("Building {Target} name tables from {SourceCount} mounted sources", target, session.Manager.Sources.Count);

        await session.CreateProgress().StartAsync(async context =>
        {
            if (target is BonesKeyword or AllKeyword)
            {
                boneTable = await BuildBoneTableAsync(session.Manager, fileSystem, nameTables, context, cancellationToken, logger);
            }

            if (target is MediaKeyword or AllKeyword)
            {
                mediaTable = BuildMediaTable(fileSystem, nameTables, context, cancellationToken, logger);
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

    private static async Task<NameTable> BuildBoneTableAsync(AssetManager manager, VirtualFileSystem fileSystem, NameTableManager nameTables, ProgressContext context, CancellationToken cancellationToken, ILogger logger)
    {
        NameTable table = GetOrCreateTable(nameTables, BoneTableName);
        List<Asset> skeletons = FindAssets(fileSystem, "*.binskeleton");
        ProgressTask task = context.AddTask("Bones", maxValue: skeletons.Count);

        if (skeletons.Count == 0)
            logger.LogWarning("No skeleton assets matching *.binskeleton were found; the {Table} name table will be empty", BoneTableName);

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

        logger.LogInformation("Updated {Table} name table to {NameCount} names from {SkeletonCount} skeletons", BoneTableName, table.Count, skeletons.Count);

        return table;
    }

    private static NameTable BuildMediaTable(VirtualFileSystem fileSystem, NameTableManager nameTables, ProgressContext context, CancellationToken cancellationToken, ILogger logger)
    {
        NameTable table = GetOrCreateTable(nameTables, WwiseMediaNameResolver.TableName);
        WwiseMediaNameResolver resolver = new();
        List<Asset> banks = FindAssets(fileSystem, "*.bnk");
        ProgressTask task = context.AddTask("Audio", maxValue: banks.Count);

        if (banks.Count == 0)
            logger.LogWarning("No sound bank assets matching *.bnk were found; the {Table} name table will be empty", WwiseMediaNameResolver.TableName);

        foreach (Asset asset in banks)
        {
            if (asset.DataSource is not Pack2File file || file.Size < 16)
            {
                task.Increment(1);
                continue;
            }

            WwiseBank bank = WwiseBank.Read(file.ReadAllBytes(cancellationToken));
            resolver.AddBank(Path.GetFileNameWithoutExtension(asset.Name), bank);
            task.Increment(1);
        }

        foreach ((uint mediaId, string mediaName) in resolver.Resolve())
        {
            table.Add(mediaId, mediaName);
        }

        NameFile.Save(GetTablePath(WwiseMediaNameResolver.TableName), table, NameFileFlags.Checksum);

        logger.LogInformation("Updated {Table} name table to {NameCount} names from {BankCount} sound banks", WwiseMediaNameResolver.TableName, table.Count, banks.Count);

        return table;
    }

    private static NameTable GetOrCreateTable(NameTableManager nameTables, string name)
    {
        if (!nameTables.TryGetTable(name, out NameTable? table))
        {
            return nameTables.CreateNameTable(name);
        }

        return table;
    }

    private static List<Asset> FindAssets(VirtualFileSystem fileSystem, string pattern) => [.. fileSystem.EnumerateFiles(null, pattern, SearchOption.AllDirectories).Select(file => file.Data).OfType<Asset>()];

    private static string GetTablePath(string name) => Path.Combine(JanitorAssetManagerFactory.NameTablesDirectory, name + ".namefile");
}
