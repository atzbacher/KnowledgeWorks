#nullable enable

using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    internal sealed class NullPublicationLookup : IPublicationLookup
    {
        internal static readonly NullPublicationLookup Instance = new();

        public Task<PublicationRecord?> TryGetByDoiAsync(string doi, bool includeCitedBy, CancellationToken ct)
            => Task.FromResult<PublicationRecord?>(null);
    }

    internal sealed class NullPmidNormalizer : IPmidNormalizer
    {
        internal static readonly NullPmidNormalizer Instance = new();

        public string? Normalize(string? raw) => raw?.Trim();
    }

    internal sealed class NullDoiNormalizer : IDoiNormalizer
    {
        internal static readonly NullDoiNormalizer Instance = new();

        public string? Normalize(string? raw) => raw?.Trim();
    }
}
