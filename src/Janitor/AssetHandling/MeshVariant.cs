using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

public readonly struct MeshVariant
{
    public required string Name { get; init; }

    public required int[] MaterialIndices { get; init; }
}
