#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Infrastructure.Hooks
{
    internal sealed class DataExtractionHookComposer : IHookComposer
    {
        private readonly HookWriter _writer;

        public DataExtractionHookComposer(HookWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public bool CanCompose(HookContext ctx) => ctx.DataExtraction is not null;

        public Task PersistAsync(string entryId, HookContext ctx, CancellationToken ct)
            => _writer.SaveDataExtractionAsync(entryId, ctx.DataExtraction!, ct);
    }
}
