using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LM.Review.Core.Models.Forms;

public sealed class FormVisibilityRule
{
    private FormVisibilityRule(
        string sourceFieldId,
        IReadOnlyList<string> expectedValues,
        bool isVisibleWhenMatches)
    {
        SourceFieldId = sourceFieldId;
        ExpectedValues = expectedValues;
        IsVisibleWhenMatches = isVisibleWhenMatches;
    }

    public string SourceFieldId { get; }

    public IReadOnlyList<string> ExpectedValues { get; }

    public bool IsVisibleWhenMatches { get; }

    public static FormVisibilityRule Create(
        string sourceFieldId,
        IEnumerable<string>? expectedValues = null,
        bool isVisibleWhenMatches = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldId);

        var normalizedSourceId = FormIdentifier.Normalize(sourceFieldId);
        if (string.IsNullOrWhiteSpace(normalizedSourceId))
        {
            throw new InvalidOperationException("Visibility rule source identifiers cannot be empty after normalization.");
        }

        var materializedValues = new List<string>();
        if (expectedValues is not null)
        {
            foreach (var value in expectedValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                materializedValues.Add(value.Trim());
            }
        }

        return new FormVisibilityRule(
            normalizedSourceId,
            new ReadOnlyCollection<string>(materializedValues),
            isVisibleWhenMatches);
    }
}
