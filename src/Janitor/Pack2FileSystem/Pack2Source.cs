using RedFox.IO.FileSystem;
using System.Diagnostics.CodeAnalysis;
using RedFox.GameExtraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Janitor.Pack2FileSystem;

/// <summary>
/// A class to hold a pack source.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Pack2Source"/> class from the provided file path.
/// </remarks>
/// <param name="path"></param>
public sealed class Pack2Source(string path) : IAssetSource
{
    private readonly List<Asset> _assets = [];
    private bool _disposed = false;

    /// <summary>
    /// Gets the path of the source file.
    /// </summary>
    public string Path { get; } = path;

    /// <inheritdoc/>
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>
    /// Gets the directories.
    /// </summary>
    public List<VirtualDirectory> Directories { get; } = [];

    /// <summary>
    /// Gets the file entries.
    /// </summary>
    public List<Pack2File> Files { get; } = [];

    /// <summary>
    /// Gets the blob streams.
    /// </summary>
    public List<Pack2Reference> References { get; } = [];

    /// <summary>
    /// Gets the classes.
    /// </summary>
    public List<string> Classes { get; } = [];

    /// <summary>
    /// Gets the resource IDs mapped to their pack files, populated during mounting.
    /// </summary>
    public Dictionary<ulong, Pack2File> Resources { get; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<Asset> Assets => _assets;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _assets.Clear();

        References.ForEach(x => x.Dispose());
        References.Clear();
        Directories.Clear();

        GC.SuppressFinalize(this);
        _disposed = true;
    }

    public bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset)
    {
        throw new NotImplementedException();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        Dispose();
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    internal void AddAsset(Asset asset) => _assets.Add(asset);
    internal void AddFile(Pack2File file) => Files.Add(file);
}
