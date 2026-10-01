namespace Janitor.Wwise;

/// <summary>
/// Reads values from a Vorbis bitstream, where bits are packed starting from the least significant bit of each byte.
/// </summary>
public ref struct OggBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _bitPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="OggBitReader"/> struct.
    /// </summary>
    /// <param name="data">The bytes to read from.</param>
    public OggBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
    }

    /// <summary>
    /// Reads up to 32 bits.
    /// </summary>
    /// <param name="count">The number of bits to read.</param>
    /// <returns>The value of the bits.</returns>
    /// <exception cref="InvalidDataException">Thrown when the read goes past the end of the data.</exception>
    public uint ReadBits(int count)
    {
        uint result = 0;

        var bitsRead = 0;

        while (bitsRead < count)
        {
            var byteIndex = _bitPosition >> 3;
            var bitIndex = _bitPosition & 7;

            if (byteIndex >= _data.Length)
                throw new InvalidDataException("Attempted to read past the end of the bitstream.");

            var bitsToRead = Math.Min(8 - bitIndex, count - bitsRead);
            var bits = (uint)(_data[byteIndex] >> bitIndex) & ((1u << bitsToRead) - 1);

            result |= bits << bitsRead;
            bitsRead += bitsToRead;
            _bitPosition += bitsToRead;
        }

        return result;
    }

    /// <summary>
    /// Reads a single bit.
    /// </summary>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    public bool ReadBit()
    {
        return ReadBits(1) != 0;
    }
}
