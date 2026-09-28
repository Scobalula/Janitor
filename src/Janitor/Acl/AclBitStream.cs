using System.Buffers.Binary;

namespace Janitor.Acl;

/// <summary>
/// Reads values from the big endian bit streams ACL packs its animated samples into.
/// </summary>
public static class AclBitStream
{
    /// <summary>
    /// Reads an unsigned value from the bit stream.
    /// </summary>
    /// <param name="buffer">The buffer containing the bit stream.</param>
    /// <param name="bitOffset">The offset, in bits from the start of the buffer, of the value.</param>
    /// <param name="bitCount">The number of bits the value is stored with, at most 32.</param>
    /// <returns>The value.</returns>
    public static uint Read(byte[] buffer, long bitOffset, int bitCount)
    {
        Span<byte> bytes = stackalloc byte[8];

        var position = (int)(bitOffset >> 3);

        buffer.AsSpan(position, Math.Min(8, buffer.Length - position)).CopyTo(bytes);

        return (uint)((BinaryPrimitives.ReadUInt64BigEndian(bytes) << (int)(bitOffset & 7)) >> (64 - bitCount));
    }

    /// <summary>
    /// Reads a value quantized to the given number of bits and normalizes it to the [0, 1] range.
    /// </summary>
    /// <param name="buffer">The buffer containing the bit stream.</param>
    /// <param name="bitOffset">The offset, in bits from the start of the buffer, of the value.</param>
    /// <param name="bitCount">The number of bits the value is stored with, at most 23.</param>
    /// <returns>The normalized value.</returns>
    public static float ReadNormalized(byte[] buffer, long bitOffset, int bitCount) => Read(buffer, bitOffset, bitCount) * (1.0f / ((1 << bitCount) - 1));

    /// <summary>
    /// Reads a full precision float from the bit stream.
    /// </summary>
    /// <param name="buffer">The buffer containing the bit stream.</param>
    /// <param name="bitOffset">The offset, in bits from the start of the buffer, of the value.</param>
    /// <returns>The float.</returns>
    public static float ReadSingle(byte[] buffer, long bitOffset) => BitConverter.UInt32BitsToSingle(Read(buffer, bitOffset, 32));
}
