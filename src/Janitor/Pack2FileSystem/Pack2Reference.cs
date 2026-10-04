using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

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
    private SafeFileHandle? _handle;
    private int _activeReads;
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
        SafeFileHandle handle;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            _handle ??= File.OpenHandle(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            handle = _handle;
            _activeReads++;
        }

        try
        {
            int consumed = 0;
            while (consumed < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = RandomAccess.Read(handle, buffer[consumed..], offset + consumed);
                if (read == 0)
                    throw new EndOfStreamException();

                consumed += read;
            }
        }
        finally
        {
            lock (_sync)
            {
                _activeReads--;
                if (_activeReads == 0)
                    Monitor.PulseAll(_sync);
            }
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
            while (_activeReads > 0)
                Monitor.Wait(_sync);

            _handle?.Dispose();
            _handle = null;
        }

        GC.SuppressFinalize(this);
    }
}
