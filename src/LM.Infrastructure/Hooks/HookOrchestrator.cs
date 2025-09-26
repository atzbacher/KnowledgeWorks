#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Public, Infrastructure-level façade. Wires internal composers and runs them.
    /// WPF calls this with an entryId and a HookContext.
    /// </summary>
    public sealed class HookOrchestrator
    {
        private readonly List<IHookComposer> _composers;

        public HookOrchestrator(IWorkSpaceService workspace)
        {
            if (workspace is null) throw new ArgumentNullException(nameof(workspace));

            var writer = new HookWriter(workspace);     // internal
            _composers = new List<IHookComposer>
            {
                new ArticleHookComposer(writer),
                new AttachmentHookComposer(writer),
                new DataExtractionHookComposer(writer),
                new ChangeLogHookComposer(writer)
            };
        }

        public async Task ProcessAsync(string entryId, HookContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(entryId)) return;
            if (ctx is null) return;

            foreach (var c in _composers)
            {
                if (c.CanCompose(ctx))
                    await c.PersistAsync(entryId, ctx, ct).ConfigureAwait(false);
            }
        }
    }
}
