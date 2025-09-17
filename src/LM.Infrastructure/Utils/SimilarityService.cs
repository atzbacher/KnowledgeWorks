using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Utils
{
    /// <summary>
    /// Phase 1: naive filename-token overlap (0..1).
    /// TODO Phase 2: extract text for PDFs/PPTX; compare shingles.
    /// </summary>
    public sealed class SimilarityService : ISimilarityService
    {
        public Task<double> ComputeFileSimilarityAsync(string filePathA, string filePathB, CancellationToken ct = default)
        {
            var a = Tokenize(Path.GetFileNameWithoutExtension(filePathA));
            var b = Tokenize(Path.GetFileNameWithoutExtension(filePathB));
            if (a.Length == 0 && b.Length == 0) return Task.FromResult(1.0);
            var inter = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
            var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
            var score = union == 0 ? 0 : (double)inter / union;
            return Task.FromResult(score);
        }

        private static string[] Tokenize(string s)
        {
            var parts = s.Split(new[] { ' ', '_', '-', '.', ',', ';', '(', ')', '[', ']', '{', '}', '+' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        }
    }
}
