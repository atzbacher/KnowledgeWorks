using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Infrastructure.Hooks;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Review
{
    internal static class ReviewChangeLogWriter
    {
        public static Task WriteAsync(
            HookOrchestrator orchestrator,
            string entryId,
            string userName,
            string action,
            IEnumerable<string> tags,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(orchestrator);
            ArgumentNullException.ThrowIfNull(tags);

            if (string.IsNullOrWhiteSpace(entryId))
            {
                return Task.CompletedTask;
            }

            var normalizedUser = string.IsNullOrWhiteSpace(userName) ? "unknown" : userName.Trim();
            var sanitizedTags = new List<string>();
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    sanitizedTags.Add(tag.Trim());
                }
            }

            if (sanitizedTags.Count == 0)
            {
                sanitizedTags.Add("action:" + action);
            }

            var changeEvent = new HookM.EntryChangeLogEvent
            {
                Action = action,
                PerformedBy = normalizedUser,
                TimestampUtc = DateTime.UtcNow,
                Details = new HookM.ChangeLogAttachmentDetails
                {
                    Tags = sanitizedTags
                }
            };

            var context = new HookContext
            {
                ChangeLog = new HookM.EntryChangeLogHook
                {
                    Events = new List<HookM.EntryChangeLogEvent> { changeEvent }
                }
            };

            return orchestrator.ProcessAsync(entryId, context, cancellationToken);
        }
    }
}
