using RedFox.Compression;

namespace Janitor.Pack2FileSystem;

internal sealed class Pack2FileStream : Stream
{
    private readonly Pack2File _file;
    private readonly (int BlobIndex, long Offset, int CompressedSize, int DecompressedSize, CompressionCodec Codec, long Start)[] _blocks;
    private byte[] _compressedBuffer;
    private byte[] _decompressedBuffer;
    private int _cachedBlockIndex = -1;
    private long _position;
    private bool _disposed;

    public override bool CanRead => !_disposed;

    public override bool CanSeek => !_disposed;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _file.Size;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _position;
        }
        set => Seek(value, SeekOrigin.Begin);
    }

    public Pack2FileStream(Pack2File file)
    {
        _file = file;

        var blocks = new List<(int BlobIndex, long Offset, int CompressedSize, int DecompressedSize, CompressionCodec Codec, long Start)>();
        var maxCompressedSize = 0;
        var maxDecompressedSize = 0;
        long start = 0;

        for (int i = 0; i < file.BufferInfo.Length;)
        {
            i += Pack2SourceReader.UnpackBufferInfo(file.BufferInfo[i..].Span, out var blobIndex, out var offset, out var compressedSize, out var decompressedSize, out var codec);

            if (decompressedSize == 0)
                continue;

            blocks.Add((blobIndex, offset, compressedSize, decompressedSize, codec, start));
            start += decompressedSize;
            maxCompressedSize = Math.Max(maxCompressedSize, compressedSize);
            maxDecompressedSize = Math.Max(maxDecompressedSize, decompressedSize);
        }

        _blocks = blocks.ToArray();
        _compressedBuffer = GC.AllocateUninitializedArray<byte>(maxCompressedSize);
        _decompressedBuffer = GC.AllocateUninitializedArray<byte>(maxDecompressedSize);
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer) => ReadCore(buffer, CancellationToken.None);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ReadCore(buffer.AsSpan(offset, count), cancellationToken));
        }
        catch (OperationCanceledException exception)
        {
            return Task.FromCanceled<int>(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<int>(exception);
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ReadCore(buffer.Span, cancellationToken));
        }
        catch (OperationCanceledException exception)
        {
            return ValueTask.FromCanceled<int>(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<int>(exception);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(_position + offset),
            SeekOrigin.End => checked(_file.Size + offset),
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (position < 0)
            throw new IOException("Cannot seek before the beginning of a Pack2 file.");

        _position = position;
        return _position;
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        _compressedBuffer = [];
        _decompressedBuffer = [];
        _cachedBlockIndex = -1;
        base.Dispose(disposing);
    }

    private int ReadCore(Span<byte> buffer, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int bytesRead = 0;
        while (bytesRead < buffer.Length && _position < _file.Size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int blockIndex = FindBlock(_position);
            if (blockIndex < 0)
                throw new EndOfStreamException("Pack2 block data ended before the file size.");

            LoadBlock(blockIndex, cancellationToken);

            var block = _blocks[blockIndex];
            int blockOffset = checked((int)(_position - block.Start));
            int copyLength = Math.Min(buffer.Length - bytesRead, block.DecompressedSize - blockOffset);
            copyLength = (int)Math.Min(copyLength, _file.Size - _position);
            _decompressedBuffer.AsSpan(blockOffset, copyLength).CopyTo(buffer[bytesRead..]);
            bytesRead += copyLength;
            _position += copyLength;
        }

        return bytesRead;
    }

    private int FindBlock(long position)
    {
        if (_cachedBlockIndex >= 0)
        {
            var cachedBlock = _blocks[_cachedBlockIndex];
            if (position >= cachedBlock.Start && position < cachedBlock.Start + cachedBlock.DecompressedSize)
                return _cachedBlockIndex;
        }

        int low = 0;
        int high = _blocks.Length - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            var block = _blocks[middle];

            if (position < block.Start)
            {
                high = middle - 1;
            }
            else if (position >= block.Start + block.DecompressedSize)
            {
                low = middle + 1;
            }
            else
            {
                return middle;
            }
        }

        return -1;
    }

    private void LoadBlock(int index, CancellationToken cancellationToken)
    {
        if (_cachedBlockIndex == index)
            return;

        var block = _blocks[index];
        var compressed = _compressedBuffer.AsSpan(0, block.CompressedSize);
        var decompressed = _decompressedBuffer.AsSpan(0, block.DecompressedSize);

        _file.Owner.References[block.BlobIndex].Read(block.Offset, compressed, cancellationToken);
        block.Codec.Decompress(compressed, decompressed);

        _cachedBlockIndex = index;
    }
}
