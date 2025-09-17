using LM.Infrastructure.Text;
using Xunit;

public class PmidNormalizerTests
{
    [Theory]
    [InlineData("PMID: 12345678", "12345678")]
    [InlineData("  987 654 321  ", "987654321")]
    [InlineData("pmid:  00123x", "00123")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Normalize_Works(string? input, string? expected)
    {
        var n = new PmidNormalizer();
        Assert.Equal(expected, n.Normalize(input));
    }
}
