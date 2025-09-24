using System.Collections.Generic;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.App.Wpf.Library
{
    public interface IAttachmentMetadataPrompt
    {
        Task<AttachmentMetadataPromptResult?> RequestMetadataAsync(AttachmentMetadataPromptContext context);
    }

    public sealed record AttachmentMetadataPromptContext(string EntryTitle, IReadOnlyList<string> FilePaths);

    public sealed record AttachmentMetadataSelection(string SourcePath,
                                                     string Title,
                                                     AttachmentKind Kind,
                                                     IReadOnlyList<string> Tags);

    public sealed record AttachmentMetadataPromptResult(IReadOnlyList<AttachmentMetadataSelection> Attachments);
}
