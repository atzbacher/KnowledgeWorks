using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LM.Review.Core.Models.Forms;

public sealed class ExtractionFormSnapshot
{
    private ExtractionFormSnapshot(
        string formId,
        string versionId,
        string capturedBy,
        DateTime capturedUtc,
        IReadOnlyDictionary<string, object?> values)
    {
        FormId = formId;
        VersionId = versionId;
        CapturedBy = capturedBy;
        CapturedUtc = capturedUtc;
        Values = values;
    }

    public string FormId { get; }

    public string VersionId { get; }

    public string CapturedBy { get; }

    public DateTime CapturedUtc { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static ExtractionFormSnapshot Create(
        string formId,
        string versionId,
        IDictionary<string, object?> values,
        string? capturedBy = null,
        DateTime? capturedUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentNullException.ThrowIfNull(values);

        var normalizedFormId = FormIdentifier.Normalize(formId);
        if (string.IsNullOrWhiteSpace(normalizedFormId))
        {
            throw new InvalidOperationException("Form identifiers cannot be empty after normalization.");
        }

        var normalizedVersionId = FormIdentifier.Normalize(versionId);
        if (string.IsNullOrWhiteSpace(normalizedVersionId))
        {
            throw new InvalidOperationException("Version identifiers cannot be empty after normalization.");
        }

        var resolvedCapturedBy = FormUserContext.ResolveUserName(capturedBy);

        DateTime resolvedCapturedUtc;
        if (capturedUtc is null)
        {
            resolvedCapturedUtc = DateTime.UtcNow;
        }
        else if (capturedUtc.Value.Kind == DateTimeKind.Utc)
        {
            resolvedCapturedUtc = capturedUtc.Value;
        }
        else
        {
            resolvedCapturedUtc = capturedUtc.Value.ToUniversalTime();
        }

        var normalizedValues = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var kvp in values)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                throw new InvalidOperationException("Snapshot values require non-empty field identifiers.");
            }

            var normalizedKey = FormIdentifier.Normalize(kvp.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                throw new InvalidOperationException("Snapshot values require identifiers that remain non-empty after normalization.");
            }

            normalizedValues[normalizedKey] = kvp.Value;
        }

        return new ExtractionFormSnapshot(
            normalizedFormId,
            normalizedVersionId,
            resolvedCapturedBy,
            resolvedCapturedUtc,
            new ReadOnlyDictionary<string, object?>(normalizedValues));
    }
}
