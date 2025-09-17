#nullable enable
using LM.HubSpoke.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.ViewModels
{
    internal static class SimLogExtensions
    {
        public static Task Maybe(
            this ISimilarityLog? log,
            string sessionId,
            string sourcePath,
            string entryId,
            double score,
            string channel,
            CancellationToken ct)
            => log is null ? Task.CompletedTask : log.LogAsync(sessionId, sourcePath, entryId, score, channel, ct);
    }
}
