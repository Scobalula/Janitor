using Janitor.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

internal class MeshLod(int index, MeshFileLayout layout)
{
    public int Index { get; } = index;

    /// <summary>Buffer region holding this LOD; tail LODs share one region.</summary>
    public MeshFileLayout Layout { get; } = layout;

    /// <summary>Byte offset of this LOD's vertices inside the region's vertex blob.</summary>
    public int VertexOffset { get; set; }

    /// <summary>Byte offset of this LOD's vertices inside the region's position-only vertex blob.</summary>
    public int PositionOnlyVertexOffset { get; set; }

    public List<MeshPrimitive> Primitives { get; } = [];
}
