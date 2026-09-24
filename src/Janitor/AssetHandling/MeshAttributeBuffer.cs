using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Janitor.AssetHandling;

public class MeshAttributeBuffer
{
    public required byte[] Buffer { get; set; }

    public required int ByteOffset { get; set; }

    public required MeshAttribute? Attribute { get; set; }

    public required int Stride { get; set; }

    public T Read<T>(int vertexIndex, int component) where T : INumber<T>
    {
        if (Attribute is null)
            return T.Zero;

        var slice = Buffer.AsSpan()[(ByteOffset + (vertexIndex * Stride) + Attribute.Value.Offset)..];

        return Attribute.Value.DataType switch
        {
            0  => T.CreateChecked(MemoryMarshal.Read<float>(slice[(component * 4)..])),
            1  => T.CreateChecked(MemoryMarshal.Read<float>(slice[(component * 4)..])),
            2  => T.CreateChecked(MemoryMarshal.Read<float>(slice[(component * 4)..])),
            3  => T.CreateChecked(MemoryMarshal.Read<float>(slice[(component * 4)..])),
            4  => T.CreateChecked(slice[component] / 255.0f),
            5  => T.CreateChecked(slice[component]),
            7  => T.CreateChecked(MemoryMarshal.Read<short>(slice[(component * 2)..])),
            8  => T.CreateChecked(MemoryMarshal.Read<short>(slice[(component * 2)..])),
            13 => T.CreateChecked(MemoryMarshal.Read<ushort>(slice[(component * 2)..])),
            15 => T.CreateChecked(MemoryMarshal.Read<uint>(slice)),
            _  => throw new NotSupportedException($"Unsupported attribute type: {Attribute.Value.DataType}"),
        };
    }
}
