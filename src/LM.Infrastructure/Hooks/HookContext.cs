#nullable enable
using HookM = LM.HubSpoke.Models;
using LM.Review.Core.Services;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Carries all optional hook inputs. Start with Article; extend later without changing callers.
    /// </summary>
    public sealed class HookContext : IReviewHookContext
    {
        public HookM.ArticleHook? Article { get; init; }
        public HookM.AttachmentHook? Attachments { get; init; }
        public HookM.EntryChangeLogHook? ChangeLog { get; init; }
    }
}
