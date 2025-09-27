using System;
using System.Collections.Generic;
using System.Linq;

namespace TabulaSharp.Models
{
    /// <summary>
    /// Represents a logical row of tokens extracted from a PDF page.
    /// </summary>
    public sealed class TabulaSharpLine
    {
        public TabulaSharpLine(double baseline, IReadOnlyList<TabulaSharpToken> tokens)
        {
            Baseline = baseline;
            Tokens = tokens ?? Array.Empty<TabulaSharpToken>();
        }

        public double Baseline { get; }

        public IReadOnlyList<TabulaSharpToken> Tokens { get; }

        public bool HasTokens => Tokens.Count > 0;

        public bool ContainsDigits => Tokens.Any(t => t.ContainsDigits());

        public TabulaSharpBoundingBox GetBounds()
        {
            if (Tokens.Count == 0)
            {
                return new TabulaSharpBoundingBox(0d, 0d, 0d, 0d);
            }

            var left = Tokens.Min(t => t.Left);
            var right = Tokens.Max(t => t.Right);
            var bottom = Tokens.Min(t => t.Bottom);
            var top = Tokens.Max(t => t.Top);
            return new TabulaSharpBoundingBox(left, bottom, right, top);
        }
    }
}
