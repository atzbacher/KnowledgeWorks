#nullable enable
using System;
using System.Collections.Generic;
using TabulaSharp.Processing;

namespace LM.App.Wpf.ViewModels.Playground
{
    internal sealed class TabulaSharpPlaygroundMode
    {
        private readonly Func<TabulaSharpOptions> _optionsFactory;

        public TabulaSharpPlaygroundMode(string name, string description, Func<TabulaSharpOptions> optionsFactory)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Mode name must be provided.", nameof(name));

            Name = name;
            Description = description ?? string.Empty;
            _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        }

        public string Name { get; }

        public string Description { get; }

        public TabulaSharpOptions CreateOptions()
        {
            var options = _optionsFactory();
            return options ?? new TabulaSharpOptions();
        }

        public static IReadOnlyList<TabulaSharpPlaygroundMode> CreateDefaults()
        {
            return new[]
            {
                new TabulaSharpPlaygroundMode(
                    "Balanced detection",
                    "Default TabulaSharp heuristics tuned for mixed document tables.",
                    static () => new TabulaSharpOptions()),
                new TabulaSharpPlaygroundMode(
                    "Dense headers",
                    "Emphasize wide column headers (5+) to surface structured summary tables.",
                    static () => new TabulaSharpOptions
                    {
                        MinimumHeaderColumns = 5,
                        MinimumDataColumns = 2,
                        RowMergeTolerance = 2.5d
                    }),
                new TabulaSharpPlaygroundMode(
                    "Tight rows",
                    "Lower the merge tolerance to keep tightly stacked rows separated.",
                    static () => new TabulaSharpOptions
                    {
                        RowMergeTolerance = 1.2d,
                        MinimumRows = 2
                    }),
                new TabulaSharpPlaygroundMode(
                    "Loose rows",
                    "Raise row tolerance to stitch sparse multi-line cells before extraction.",
                    static () => new TabulaSharpOptions
                    {
                        RowMergeTolerance = 4.0d,
                        MinimumHeaderColumns = 3,
                        MinimumDataColumns = 1
                    })
            };
        }
    }
}
