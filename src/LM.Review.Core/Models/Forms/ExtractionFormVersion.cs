using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LM.Review.Core.Models.Forms;

public sealed class ExtractionFormVersion
{
    private ExtractionFormVersion(
        string versionId,
        ExtractionForm form,
        string createdBy,
        DateTime createdUtc,
        IReadOnlyDictionary<string, string> metadata)
    {
        VersionId = versionId;
        Form = form;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
        Metadata = metadata;
    }

    public string VersionId { get; }

    public ExtractionForm Form { get; }

    public string CreatedBy { get; }

    public DateTime CreatedUtc { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public static ExtractionFormVersion Create(
        string versionId,
        ExtractionForm form,
        IDictionary<string, string>? metadata = null,
        string? createdBy = null,
        DateTime? createdUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentNullException.ThrowIfNull(form);

        var normalizedVersionId = FormIdentifier.Normalize(versionId);
        if (string.IsNullOrWhiteSpace(normalizedVersionId))
        {
            throw new InvalidOperationException("Version identifiers cannot be empty after normalization.");
        }

        var resolvedCreatedBy = FormUserContext.ResolveUserName(createdBy);

        DateTime resolvedCreatedUtc;
        if (createdUtc is null)
        {
            resolvedCreatedUtc = DateTime.UtcNow;
        }
        else if (createdUtc.Value.Kind == DateTimeKind.Utc)
        {
            resolvedCreatedUtc = createdUtc.Value;
        }
        else
        {
            resolvedCreatedUtc = createdUtc.Value.ToUniversalTime();
        }

        var materializedMetadata = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);

        return new ExtractionFormVersion(
            normalizedVersionId,
            form,
            resolvedCreatedBy,
            resolvedCreatedUtc,
            new ReadOnlyDictionary<string, string>(materializedMetadata));
    }
}
