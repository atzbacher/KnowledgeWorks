#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace LM.Infrastructure.Hooks
{
    /// <summary>Internal pluggable composer: decides if it can handle a context and persists its hook.</summary>
    internal interface IHookComposer
    {
        bool CanCompose(HookContext ctx);
        Task PersistAsync(string entryId, HookContext ctx, CancellationToken ct);
    }
}
