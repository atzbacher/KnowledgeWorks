#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Infrastructure.Hooks
{
    internal sealed class ChangeLogHookComposer : IHookComposer
    {
        private readonly HookWriter _writer;

        public ChangeLogHookComposer(HookWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public bool CanCompose(HookContext ctx)
            => ctx.ChangeLog is { Events.Count: > 0 };

        public Task PersistAsync(string entryId, HookContext ctx, CancellationToken ct)
            => _writer.AppendChangeLogAsync(entryId, ctx.ChangeLog!, ct);
    }
}
