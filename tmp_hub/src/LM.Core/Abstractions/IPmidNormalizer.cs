#nullable enable
namespace LM.Core.Abstractions
{
    /// <summary>Normalizes a PMID string to digits-only (e.g., "PMID: 123 456" → "123456").</summary>
    public interface IPmidNormalizer
    {
        string? Normalize(string? raw);
    }
}
