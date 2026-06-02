using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Janitor.AssetHandling;

public struct MeshPrimitive
{
    public required int LODIndex { get; set; }
    public required int VertexCount { get; set; }

    public required int FaceCount { get; set; }

    public required int VertexOffset { get; set; }

    public required int FaceOffset { get; set; }

    public required int PartOffset { get; set; }

    public required int MaterialIndex { get; set; }

    public required int FaceIndexSize { get; set; }

    public required Vector3 Min { get; set; }
    public required Vector3 Max { get; set; }

    public required Vector3 Center { get; set; }

    public required float Scale { get; set; }

    public required int VertexSize { get; set; }

    public required int VertexPositionSize { get; set; }

    public required MeshAttribute[] Attributes { get; init; }
}
