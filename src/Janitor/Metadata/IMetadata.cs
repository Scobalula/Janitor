using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Janitor.Metadata;

public interface IMetadata
{
    /// <summary>
    /// Parses the metadata from the provided buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the metadata.</param>
    void Parse(ReadOnlySpan<byte> buffer);
}
