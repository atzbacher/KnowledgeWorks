using System;
using LM.HubSpoke.Models;
using Xunit;

public class EntryNotesHookTests
{
    [Fact]
    public void ToDisplayString_HandlesMissingAndInvalidValues()
    {
        var summary = new LitSearchNoteSummary
        {
            Title = "  Example  ",
            Query = "   ",
            Provider = string.Empty,
            CreatedBy = "   ",
            CreatedUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local),
            RunCount = -5,
            DerivedFromEntryId = "  ENTRY-42  ",
            LatestRun = new LitSearchNoteRunSummary
            {
                ExecutedBy = null,
                RunUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local),
                TotalHits = -10,
                From = null,
                To = null
            }
        };

        var text = summary.ToDisplayString();

        Assert.Contains("Title: Example", text);
        Assert.Contains("Query: unknown", text);
        Assert.Contains("Provider: unknown", text);
        Assert.Contains("Created by unknown on unknown.", text);
        Assert.Contains("Run count: 0", text);
        Assert.Contains("Latest run executed by unknown on unknown (hits: 0).", text);
        Assert.Contains("Derived from entry ENTRY-42.", text);
    }
}
