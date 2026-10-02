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
    private readonly object _sync = new();
    private Stream? _stream;
    private bool _disposed;

    /// <summary>
    /// Gets the path of the source file.
    /// </summary>
    public string Path { get; } = path;

    public void Read(long offset, Span<byte> buffer)
    {
        Read(offset, buffer, CancellationToken.None);
    }

    public void Read(long offset, Span<byte> buffer, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            _stream ??= File.OpenRead(Path);
            _stream.Position = offset;
            _stream.ReadExactly(buffer);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream?.Dispose();
            _stream = null;
        }

        GC.SuppressFinalize(this);
    }
}
