#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Infrastructure.Hooks
{
    internal sealed class AttachmentHookComposer : IHookComposer
    {
        private readonly HookWriter _writer;

        public AttachmentHookComposer(HookWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public bool CanCompose(HookContext ctx) => ctx.Attachments is not null;

        public Task PersistAsync(string entryId, HookContext ctx, CancellationToken ct)
            => _writer.SaveAttachmentsAsync(entryId, ctx.Attachments!, ct);
    }
}
