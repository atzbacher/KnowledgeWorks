// LM.Infrastructure.Utils.ContentAwareSimilarityService.cs
#nullable enable
using LM.Core.Abstractions;

namespace LM.Infrastructure.Utils
{
    // Public class already in API — only implementation changes
    public sealed class ContentAwareSimilarityService : ISimilarityService
    {
        private readonly IContentExtractor _extractor;
        private const int K = 5; // shingle size (tweak 3..10 if needed)

        public ContentAwareSimilarityService(IContentExtractor extractor) => _extractor = extractor;

        public async Task<double> ComputeFileSimilarityAsync(string filePathA, string filePathB, CancellationToken ct = default)
        {
            var a = await _extractor.ExtractTextAsync(filePathA, ct);
            var b = await _extractor.ExtractTextAsync(filePathB, ct);

            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return 0.0;

            var A = Shingles(a, K);
            var B = Shingles(b, K);
            if (A.Count == 0 || B.Count == 0) return 0.0;

            var inter = 0;
            foreach (var s in A) if (B.Contains(s)) inter++;
            var union = A.Count + B.Count - inter;

            return union == 0 ? 0.0 : (double)inter / union;
        }

        private static HashSet<string> Shingles(string s, int k)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (s.Length < k) return set;
            for (int i = 0; i <= s.Length - k; i++)
                set.Add(s.AsSpan(i, k).ToString());
            return set;
        }
    }
}
