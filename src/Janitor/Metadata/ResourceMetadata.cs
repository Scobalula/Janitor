using RedFox.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Janitor.Metadata;

/// <summary>
/// 
/// </summary>
public class ResourceMetadata : IMetadata
{
    public ulong Id { get; set; }
    public List<ulong> Dependencies { get; set; } = [];

    /// <inheritdoc/>
    public void Parse(ReadOnlySpan<byte> buffer)
    {
        var reader = new SpanReader(buffer);

        var version = reader.Read<uint>();

        if (version != 3)
            throw new NotSupportedException($"r::ResourceMetadata Version: {version}");

        var idVersion = reader.Read<uint>();

        if (idVersion != 1)
            throw new NotSupportedException($"r::ResourceMetadata ID Version: {idVersion}");

        Id = reader.Read<ulong>();
    }
}
