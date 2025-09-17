#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LM.Infrastructure.Hooks
{
    /// <summary>Persists article.json when HookContext carries an ArticleHook.</summary>
    internal sealed class ArticleHookComposer : IHookComposer
    {
        private readonly HookWriter _writer;

        public ArticleHookComposer(HookWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public bool CanCompose(HookContext ctx) => ctx.Article is not null;

        public Task PersistAsync(string entryId, HookContext ctx, CancellationToken ct)
            => _writer.SaveArticleAsync(entryId, ctx.Article!, ct);
    }
}
