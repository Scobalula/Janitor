using Janitor.Pack2FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace Janitor;

public class ResourceTableService
{
    /// <summary>
    /// Gets a dictionary of resource IDs and their corresponding <see cref="Pack2File"/> instances.
    /// </summary>
    public Dictionary<ulong, Pack2File> Resources { get; } = [];
}
