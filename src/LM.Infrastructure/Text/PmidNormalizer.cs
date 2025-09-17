#nullable enable
using LM.Core.Abstractions;

namespace LM.Infrastructure.Text
{
    /// <summary>Digits-only PMID normalizer. Trims, strips "pmid:" prefix, removes non-digits.</summary>
    public sealed class PmidNormalizer : IPmidNormalizer
    {
        public string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (s.StartsWith("pmid:", System.StringComparison.OrdinalIgnoreCase)) s = s[5..].Trim();

            // fast digits-only copy
            System.Span<char> buf = stackalloc char[s.Length];
            var j = 0;
            foreach (var ch in s)
                if (char.IsDigit(ch)) buf[j++] = ch;

            if (j == 0) return null;
            return new string(buf[..j]);
        }
    }
}
