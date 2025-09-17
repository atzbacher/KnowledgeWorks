using System.Text.Json;
using LM.HubSpoke.Models;
using Xunit;

public class ArticleHookJsonTests
{
    [Fact]
    public void AbstractSection_ClrIsText_ButJsonIsContent()
    {
        var hook = new ArticleHook
        {
            Abstract = new ArticleAbstract
            {
                Sections =
                {
                    new AbstractSection { Label = "Background", Text = "Alpha" }
                },
                Text = "Plain"
            }
        };

        var json = JsonSerializer.Serialize(hook);

        // JSON still uses "content" (backwards-compatible on disk)
        Assert.Contains("\"content\":\"Alpha\"", json);

        // Round-trip to ensure JSON -> CLR maps back to Text
        var roundTripped = JsonSerializer.Deserialize<ArticleHook>(json);
        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.Abstract);
        Assert.Single(roundTripped.Abstract!.Sections);
        Assert.Equal("Alpha", roundTripped.Abstract!.Sections[0].Text);
    }
}
