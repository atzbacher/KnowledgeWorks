#nullable enable
using System;
using LM.App.Wpf.Library.Search;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryInlineDirectiveParserTests
    {
        [Fact]
        public void Parse_WithFullTextAndDateDirectives_ExtractsValues()
        {
            var parser = new LibraryInlineDirectiveParser();

            var result = parser.Parse("cancer FULLTEXT:\"immune response\" FROM:01/2024 TO:2025");

            Assert.Equal("cancer", result.MetadataQuery);
            Assert.True(result.HasFullTextDirective);
            Assert.Equal("immune response", result.FullTextQuery);
            Assert.True(result.HasFromDirective);
            Assert.Equal(new DateTime(2024, 1, 1), result.FromDate);
            Assert.True(result.HasToDirective);
            Assert.Equal(new DateTime(2025, 12, 31), result.ToDate);
        }

        [Fact]
        public void Parse_WithMonthRangeToDirective_UsesLastDayOfMonth()
        {
            var parser = new LibraryInlineDirectiveParser();

            var result = parser.Parse("FULLTEXT:insight TO:02/2024");

            Assert.True(result.HasToDirective);
            Assert.Equal(new DateTime(2024, 2, 29), result.ToDate);
            Assert.Equal(string.Empty, result.MetadataQuery);
            Assert.Equal("insight", result.FullTextQuery);
        }

        [Fact]
        public void Parse_WithInvalidFromDirective_FlagsDirectiveButNoDate()
        {
            var parser = new LibraryInlineDirectiveParser();

            var result = parser.Parse("FROM:32/13/2024");

            Assert.True(result.HasFromDirective);
            Assert.Null(result.FromDate);
            Assert.Equal(string.Empty, result.MetadataQuery);
        }
    }
}

