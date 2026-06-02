using Janitor.Metadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

internal class MeshLod(MeshFileLayout layout)
{
    public MeshFileLayout Layout { get; } = layout;

    public List<MeshPrimitive> Primitives { get; } = [];
}
