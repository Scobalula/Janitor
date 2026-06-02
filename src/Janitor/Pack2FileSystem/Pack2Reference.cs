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
/// Initializes a new instance of the <see cref="Pack2Reference"/> class from the provided file path.
/// </remarks>
/// <param name="path"></param>
public class Pack2Reference(string path) : IDisposable
{
    private Stream? _stream;

    /// <summary>
    /// Gets the path of the source file.
    /// </summary>
    public string Path { get; } = path;

    public void Read(long offset, Span<byte> buffer)
    {
        _stream ??= File.OpenRead(Path);
        _stream.Position = offset;
        _stream.ReadExactly(buffer);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _stream?.Dispose();
    }
}
