#nullable enable
using System;
using System.Collections.Generic;
using LM.App.Wpf.Services.Review;
using LM.App.Wpf.ViewModels.Dialogs;
using Xunit;

namespace LM.App.Wpf.Tests.Review
{
    public sealed class LitSearchRunPickerViewModelTests
    {
        [Fact]
        public void Initialize_WithOptions_PopulatesEntriesAndSelection()
        {
            var options = new List<LitSearchRunOption>
            {
                new("entry-alpha", "Alpha", "query alpha", "abs-alpha", "entries/entry-alpha/hooks/litsearch.json", new[]
                {
                    new LitSearchRunOptionRun("run-1", DateTime.SpecifyKind(new DateTime(2024, 1, 1, 9, 0, 0), DateTimeKind.Utc), 42, "alice", true, "alpha-run-1.json", "hooks/alpha-run-1.json"),
                    new LitSearchRunOptionRun("run-2", DateTime.SpecifyKind(new DateTime(2024, 2, 1, 9, 0, 0), DateTimeKind.Utc), 64, "alice", false, "alpha-run-2.json", "hooks/alpha-run-2.json")
                }),
                new("entry-bravo", "Bravo", "query bravo", "abs-bravo", "entries/entry-bravo/hooks/litsearch.json", new[]
                {
                    new LitSearchRunOptionRun("run-3", DateTime.SpecifyKind(new DateTime(2024, 3, 1, 9, 0, 0), DateTimeKind.Utc), 12, "bob", false, "bravo-run-3.json", "hooks/bravo-run-3.json")
                })
            };

            var viewModel = new LitSearchRunPickerViewModel();

            viewModel.Initialize(options);

            Assert.True(viewModel.HasEntries);
            Assert.Equal(2, viewModel.Entries.Count);
            Assert.NotNull(viewModel.SelectedEntry);
            Assert.Equal("Alpha", viewModel.SelectedEntry!.Label);
            Assert.NotNull(viewModel.SelectedRun);
            Assert.Equal("run-1", viewModel.SelectedRun!.RunId);
            Assert.True(viewModel.ConfirmCommand.CanExecute(null));
        }

        [Fact]
        public void BuildSelection_UsesCurrentSelection()
        {
            var options = new List<LitSearchRunOption>
            {
                new("entry-alpha", "Alpha", "query alpha", "abs-alpha", "entries/entry-alpha/hooks/litsearch.json", new[]
                {
                    new LitSearchRunOptionRun("run-1", DateTime.SpecifyKind(new DateTime(2024, 1, 1, 9, 0, 0), DateTimeKind.Utc), 42, "alice", true, "alpha-run-1.json", "hooks/alpha-run-1.json"),
                    new LitSearchRunOptionRun("run-2", DateTime.SpecifyKind(new DateTime(2024, 2, 1, 9, 0, 0), DateTimeKind.Utc), 64, "alice", false, "alpha-run-2.json", "hooks/alpha-run-2.json")
                }),
                new("entry-bravo", "Bravo", "query bravo", "abs-bravo", "entries/entry-bravo/hooks/litsearch.json", new[]
                {
                    new LitSearchRunOptionRun("run-3", DateTime.SpecifyKind(new DateTime(2024, 3, 1, 9, 0, 0), DateTimeKind.Utc), 12, "bob", false, "bravo-run-3.json", "hooks/bravo-run-3.json")
                })
            };

            var viewModel = new LitSearchRunPickerViewModel();
            viewModel.Initialize(options);

            viewModel.SelectedEntry = viewModel.Entries[1];
            viewModel.SelectedRun = viewModel.SelectedEntry.Runs[0];

            var selection = viewModel.BuildSelection();

            Assert.NotNull(selection);
            Assert.Equal("entry-bravo", selection!.EntryId);
            Assert.Equal("run-3", selection.RunId);
            Assert.Equal("entries/entry-bravo/hooks/litsearch.json", selection.HookRelativePath);
        }

        [Fact]
        public void ConfirmCommand_DisabledWhenSelectionMissing()
        {
            var options = new List<LitSearchRunOption>();
            var viewModel = new LitSearchRunPickerViewModel();

            viewModel.Initialize(options);

            Assert.False(viewModel.HasEntries);
            Assert.Null(viewModel.SelectedEntry);
            Assert.Null(viewModel.SelectedRun);
            Assert.False(viewModel.ConfirmCommand.CanExecute(null));
            Assert.Null(viewModel.BuildSelection());
        }
    }
}
