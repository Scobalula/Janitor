namespace Janitor.Wwise;

/// <summary>
/// Writes values to a Vorbis bitstream, where bits are packed starting from the least significant bit of each byte.
/// </summary>
/// <param name="initialCapacity">The number of bytes to reserve up front.</param>
public sealed class OggBitWriter(int initialCapacity)
{
    private byte[] _buffer = new byte[Math.Max(initialCapacity, 1)];
    private int _bitPosition;

    /// <summary>
    /// Writes up to 32 bits.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="count">The number of bits of the value to write.</param>
    public void WriteBits(uint value, int count)
    {
        var requiredBytes = (_bitPosition + count + 7) >> 3;

        if (requiredBytes > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, requiredBytes));

        var bitsWritten = 0;

        while (bitsWritten < count)
        {
            var byteIndex = _bitPosition >> 3;
            var bitIndex = _bitPosition & 7;
            var bitsToWrite = Math.Min(8 - bitIndex, count - bitsWritten);
            var bits = (value >> bitsWritten) & ((1u << bitsToWrite) - 1);

            _buffer[byteIndex] |= (byte)(bits << bitIndex);
            bitsWritten += bitsToWrite;
            _bitPosition += bitsToWrite;
        }
    }

    /// <summary>
    /// Copies bits from a source, which is used to carry the audio data of a packet over unchanged.
    /// </summary>
    /// <param name="source">The bytes to copy from.</param>
    /// <param name="bitOffset">The number of bits to skip at the start of the source.</param>
    /// <param name="bitCount">The number of bits to copy.</param>
    public void CopyBits(ReadOnlySpan<byte> source, int bitOffset, int bitCount)
    {
        var reader = new OggBitReader(source);

        for (var skipped = 0; skipped < bitOffset; skipped++)
            reader.ReadBit();

        for (; bitCount >= 32; bitCount -= 32)
            WriteBits(reader.ReadBits(32), 32);

        WriteBits(reader.ReadBits(bitCount), bitCount);
    }

    /// <summary>
    /// Gets the bits written so far, padded with zero bits to a whole number of bytes.
    /// </summary>
    /// <returns>The written bytes.</returns>
    public byte[] ToArray()
    {
        return _buffer[..((_bitPosition + 7) >> 3)];
    }
}
