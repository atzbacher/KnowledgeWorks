#nullable enable
using LM.Core.Models.DataExtraction;
using LM.Review.Core.DataExtraction;
using Xunit;

namespace LM.Infrastructure.Tests.Metadata.EvidenceExtraction
{
    public sealed class TableVocabularyTests
    {
        [Theory]
        [InlineData("Baseline characteristics", TableClassificationKind.Baseline)]
        [InlineData("Primary outcome", TableClassificationKind.Outcome)]
        public void ClassifyTitle_UsesKnownKeywords(string title, TableClassificationKind expected)
        {
            var actual = TableVocabulary.ClassifyTitle(title);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Baseline age", TableRowRole.Baseline)]
        [InlineData("Mortality at 30 days", TableRowRole.Outcome)]
        [InlineData("Visit:", TableRowRole.Header)]
        public void ClassifyRowLabel_ReturnsExpectedRole(string label, TableRowRole expected)
        {
            var actual = TableVocabulary.ClassifyRowLabel(label);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Treatment arm", TableColumnRole.Intervention)]
        [InlineData("Population", TableColumnRole.Population)]
        [InlineData("Follow-up", TableColumnRole.Timepoint)]
        public void ClassifyColumnHeader_UsesDictionaryAndRegex(string header, TableColumnRole expected)
        {
            var actual = TableVocabulary.ClassifyColumnHeader(header);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("Placebo group", "placebo")]
        [InlineData("Control arm", "arm")]
        [InlineData("Mortality rate", "mortality")]
        public void DetectionHelpers_ReturnExpectedHints(string token, string expected)
        {
            var population = TableVocabulary.TryDetectPopulation(token);
            var endpoint = TableVocabulary.TryDetectEndpoint(token);

            if (expected is "mortality")
            {
                Assert.Equal(expected, endpoint);
            }
            else
            {
                Assert.Equal(expected, population);
            }
        }
    }
}
