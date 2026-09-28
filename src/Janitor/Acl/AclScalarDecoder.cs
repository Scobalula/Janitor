using System.Buffers.Binary;

namespace Janitor.Acl;

/// <summary>
/// Decompresses uniformly sampled ACL 2.1 scalar float tracks into their exact samples, without interpolation.
/// </summary>
/// <param name="buffer">The compressed tracks buffer, starting at its raw buffer header.</param>
public sealed class AclScalarDecoder(byte[] buffer)
{
    private const int ScalarHeaderOffset = AclTracksHeader.Size;

    private static readonly byte[] BitRateBitCounts = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 32];

    private readonly AclTracksHeader _header = new(buffer);

    /// <summary>
    /// Decompresses every sample of every track. Each track holds one sample per clip sample when animated, or a single sample when constant.
    /// </summary>
    /// <returns>The decompressed tracks, in the order they were compressed.</returns>
    /// <exception cref="InvalidDataException">Thrown when the buffer does not contain compressed single float tracks.</exception>
    /// <exception cref="NotSupportedException">Thrown when the buffer uses a version or feature that is not supported.</exception>
    public float[][] Decompress()
    {
        _header.EnsureSupported(AclTrackType.Float1);

        var bitsPerFrame = ReadInt32(ScalarHeaderOffset);
        var metadataOffset = ScalarHeaderOffset + ReadInt32(ScalarHeaderOffset + 4);
        var constantOffset = ScalarHeaderOffset + ReadInt32(ScalarHeaderOffset + 8);
        var rangeOffset = ScalarHeaderOffset + ReadInt32(ScalarHeaderOffset + 12);
        var animatedBitOffset = (ScalarHeaderOffset + ReadInt32(ScalarHeaderOffset + 16)) * 8L;
        var tracks = new float[_header.TrackCount][];

        for (var track = 0; track < tracks.Length; track++)
        {
            var bitCount = BitRateBitCounts[buffer[metadataOffset + track]];

            if (bitCount == 0)
            {
                tracks[track] = [ReadSingle(constantOffset)];
                constantOffset += 4;
                continue;
            }

            // Raw samples are stored as full floats, quantized samples are normalized within the range of the track.
            var isRaw = bitCount == 32;
            var min = isRaw ? 0.0f : ReadSingle(rangeOffset);
            var extent = isRaw ? 0.0f : ReadSingle(rangeOffset + 4);

            tracks[track] = new float[_header.SampleCount];

            for (var sample = 0; sample < tracks[track].Length; sample++)
            {
                var bitOffset = animatedBitOffset + (long)sample * bitsPerFrame;

                tracks[track][sample] = isRaw ? AclBitStream.ReadSingle(buffer, bitOffset) : AclBitStream.ReadNormalized(buffer, bitOffset, bitCount) * extent + min;
            }

            rangeOffset += isRaw ? 0 : 8;
            animatedBitOffset += bitCount;
        }

        return tracks;
    }

    private int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset));

    private float ReadSingle(int offset) => BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(offset));
}
