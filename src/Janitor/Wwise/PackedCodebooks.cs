namespace Janitor.Wwise;

/// <summary>
/// The library of Vorbis codebooks that Wwise packs into its own compact format and references from its media by index.
/// </summary>
public sealed class PackedCodebooks
{
    private const string ResourceName = "Janitor.Wwise.packed_codebooks.bin";

    private static readonly Lazy<PackedCodebooks> _default = new(Load);

    private readonly int[] _offsets;
    private readonly byte[] _data;

    /// <summary>
    /// Gets the codebook library that ships with Janitor.
    /// </summary>
    public static PackedCodebooks Default => _default.Value;

    private PackedCodebooks(int[] offsets, byte[] data)
    {
        _offsets = offsets;
        _data = data;
    }

    /// <summary>
    /// Gets a codebook in Wwise's packed format.
    /// </summary>
    /// <param name="index">The index of the codebook in the library.</param>
    /// <returns>The bytes of the packed codebook.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the library.</exception>
    /// <exception cref="InvalidDataException">Thrown when the library holds no codebook for the index.</exception>
    public ReadOnlySpan<byte> GetCodebook(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _offsets.Length);

        var offset = _offsets[index];

        if (offset < 0)
            throw new InvalidDataException($"The codebook library holds no codebook for index {index}.");

        var end = _data.Length;

        for (var next = index + 1; next < _offsets.Length; next++)
        {
            if (_offsets[next] >= 0)
            {
                end = _offsets[next];

                break;
            }
        }

        return _data.AsSpan(offset, end - offset);
    }

    private static PackedCodebooks Load()
    {
        using var stream = typeof(PackedCodebooks).Assembly.GetManifestResourceStream(ResourceName) ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new BinaryReader(stream);

        var entryCount = reader.ReadInt32();
        var dataSize = reader.ReadInt32();
        var offsets = new int[entryCount];

        for (var i = 0; i < entryCount; i++)
            offsets[i] = reader.ReadInt32();

        return new PackedCodebooks(offsets, reader.ReadBytes(dataSize));
    }
}
