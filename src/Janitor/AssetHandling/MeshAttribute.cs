using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor.AssetHandling;

public record struct MeshAttribute(
    bool PositionAttribute,
    int DataType,
    int Usage,
    int Offset,
    int Size);