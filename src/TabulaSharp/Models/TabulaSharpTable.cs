using System;
using System.Collections.Generic;

namespace TabulaSharp.Models
{
    /// <summary>
    /// Represents an extracted table including its normalized rows and bounds.
    /// </summary>
    public sealed class TabulaSharpTable
    {
        public TabulaSharpTable(IReadOnlyList<IReadOnlyList<string>> rows, TabulaSharpBoundingBox bounds)
        {
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Bounds = bounds;
        }

        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        public TabulaSharpBoundingBox Bounds { get; }
    }
}
