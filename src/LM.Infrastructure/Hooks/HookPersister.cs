#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Public façade for persisting hook JSON. Wraps the internal HookWriter.
    /// </summary>
    public sealed class HookPersister
    {
        private readonly HookWriter _writer; // internal class

        public HookPersister(IWorkSpaceService workspace)
        {
            if (workspace is null) throw new ArgumentNullException(nameof(workspace));
            _writer = new HookWriter(workspace);
        }

        public async Task SaveArticleIfAnyAsync(string entryId, HookM.ArticleHook? hook, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId) || hook is null) return;
            await _writer.SaveArticleAsync(entryId, hook, ct);
        }
    }
}
