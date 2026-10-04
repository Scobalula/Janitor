using Janitor.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

internal class MeshLod(int index, MeshFileLayout layout)
{
    public int Index { get; } = index;

    public MeshFileLayout Layout { get; } = layout;

    public int VertexOffset { get; set; }

    public int PositionOnlyVertexOffset { get; set; }

    public List<MeshPrimitive> Primitives { get; } = [];
}
