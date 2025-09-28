using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LM.App.Wpf.ViewModels.Library;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class DataExtractionTableViewModelTests
    {
        [Fact]
        public void ToTsv_MergesSignColumnsWithInequalityTokens()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new List<string> { "<=", "5" },
                new List<string> { "≥", "10" },
                new List<string> { "±", "0.3" }
            };

            var viewModel = CreateViewModel(rows, columnCount: 2);

            var tsv = viewModel.ToTsv();
            var lines = tsv.Split(Environment.NewLine, StringSplitOptions.None);

            Assert.Collection(
                lines,
                header => Assert.Equal("Column 1", header),
                first => Assert.Equal("<= 5", first),
                second => Assert.Equal("≥ 10", second),
                third => Assert.Equal("± 0.3", third));

            Assert.All(lines.Skip(1), line => Assert.DoesNotContain('\t', line));
        }

        private static DataExtractionTableViewModel CreateViewModel(
            IReadOnlyList<IReadOnlyList<string>> rows,
            int columnCount)
        {
            var constructor = typeof(DataExtractionTableViewModel)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();

            return (DataExtractionTableViewModel)constructor.Invoke(new object?[]
            {
                1,
                1,
                DataExtractionMode.Stream,
                TableDetectionStrategy.Auto,
                null,
                rows,
                columnCount,
                string.Empty
            });
        }
    }
}
