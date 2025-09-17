using LM.Infrastructure.Utils;
using Xunit;

public class TagMergerTests
{
    [Fact]
    public void Merge_Adds_Distinct_Tags_Ignore_Case_And_Whitespace()
    {
        var merged = TagMerger.Merge("alpha, beta", new[] { "Beta", "Gamma" });
        Assert.Equal("alpha, beta, Gamma", merged, ignoreCase: true);
    }
}