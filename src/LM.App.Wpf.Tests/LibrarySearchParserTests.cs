using System;
using System.Collections.Generic;
using LM.App.Wpf.Library.Search;
using LM.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests
{
    public sealed class LibrarySearchParserTests
    {
        private readonly LibrarySearchParser _parser = new();
        private readonly LibrarySearchEvaluator _evaluator = new();

        [Fact]
        public void Parse_ReturnsNullForEmptyQuery()
        {
            Assert.Null(_parser.Parse(null));
            Assert.Null(_parser.Parse(string.Empty));
            Assert.Null(_parser.Parse("   "));
        }

        [Fact]
        public void ImplicitAnd_MatchesEntriesWithAllTerms()
        {
            var node = _parser.Parse("title:heart author:smith");
            Assert.NotNull(node);

            var matching = new Entry
            {
                Title = "Heart Health", 
                Authors = new List<string> { "Jane Smith" }
            };
            var nonMatching = new Entry
            {
                Title = "Heart Health",
                Authors = new List<string> { "Alex Johnson" }
            };

            Assert.True(_evaluator.Matches(matching, node));
            Assert.False(_evaluator.Matches(nonMatching, node));
        }

        [Fact]
        public void BooleanOperators_RespectGrouping()
        {
            var node = _parser.Parse("title:heart AND (author:smith OR author:doe)");
            Assert.NotNull(node);

            var smith = new Entry { Title = "Heart Insights", Authors = new List<string> { "John Smith" } };
            var doe = new Entry { Title = "Heart Insights", Authors = new List<string> { "Mary Doe" } };
            var other = new Entry { Title = "Heart Insights", Authors = new List<string> { "Alex Johnson" } };

            Assert.True(_evaluator.Matches(smith, node));
            Assert.True(_evaluator.Matches(doe, node));
            Assert.False(_evaluator.Matches(other, node));
        }

        [Fact]
        public void NotOperator_ExcludesMatchingEntries()
        {
            var node = _parser.Parse("NOT internal:true");
            Assert.NotNull(node);

            var internalEntry = new Entry { IsInternal = true };
            var externalEntry = new Entry { IsInternal = false };

            Assert.False(_evaluator.Matches(internalEntry, node));
            Assert.True(_evaluator.Matches(externalEntry, node));
        }

        [Fact]
        public void NumericAndDateOperators_FilterCorrectly()
        {
            var node = _parser.Parse("year:>=2020 AND addedon:2024-03-15");
            Assert.NotNull(node);

            var matching = new Entry
            {
                Year = 2021,
                AddedOnUtc = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc)
            };
            var wrongYear = new Entry
            {
                Year = 2019,
                AddedOnUtc = matching.AddedOnUtc
            };
            var wrongDate = new Entry
            {
                Year = matching.Year,
                AddedOnUtc = new DateTime(2024, 3, 14, 0, 0, 0, DateTimeKind.Utc)
            };

            Assert.True(_evaluator.Matches(matching, node));
            Assert.False(_evaluator.Matches(wrongYear, node));
            Assert.False(_evaluator.Matches(wrongDate, node));
        }

        [Fact]
        public void SetOperators_HandleTags()
        {
            var node = _parser.Parse("tags:cardio,vascular");
            Assert.NotNull(node);

            var withCardio = new Entry { Tags = new List<string> { "cardiology", "urgent" } };
            var withVascular = new Entry { Tags = new List<string> { "vascular clinic" } };
            var unrelated = new Entry { Tags = new List<string> { "oncology" } };

            Assert.True(_evaluator.Matches(withCardio, node));
            Assert.True(_evaluator.Matches(withVascular, node));
            Assert.False(_evaluator.Matches(unrelated, node));
        }
    }
}
