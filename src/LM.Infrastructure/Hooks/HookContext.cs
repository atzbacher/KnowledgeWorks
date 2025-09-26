#nullable enable
using HookM = LM.HubSpoke.Models;

namespace LM.Infrastructure.Hooks
{
    /// <summary>
    /// Carries all optional hook inputs. Start with Article; extend later without changing callers.
    /// </summary>
    public sealed class HookContext
    {
        public HookM.ArticleHook? Article { get; init; }
        public HookM.AttachmentHook? Attachments { get; init; }
        public HookM.DataExtractionHook? DataExtraction { get; init; }
        public HookM.EntryChangeLogHook? ChangeLog { get; init; }
    }
}
