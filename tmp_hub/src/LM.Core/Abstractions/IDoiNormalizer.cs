#nullable enable
namespace LM.Core.Abstractions
{
    /// <summary>
    /// Extracts and normalizes a DOI from arbitrary text or metadata.
    /// Returns null when no valid DOI can be found.
    /// </summary>
    public interface IDoiNormalizer
    {
        string? Normalize(string? raw);
    }
}
