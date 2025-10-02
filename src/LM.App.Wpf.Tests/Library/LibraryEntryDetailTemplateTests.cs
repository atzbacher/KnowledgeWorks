#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LM.App.Wpf.ViewModels;
using LM.App.Wpf.ViewModels.Library;
using LM.Core.Models;
using Xunit;

namespace LM.App.Wpf.Tests.Library
{
    public sealed class LibraryEntryDetailTemplateTests
    {
        [Fact]
        public async Task Template_WithData_ShowsSectionContent()
        {
            var entry = CreatePopulatedEntry();
            var result = new LibrarySearchResult(entry, 0.82, "highlight");
            var host = new StubLibraryDetailViewModel(includeData: true);

            await RunOnStaThreadAsync(() =>
            {
                EnsureApplication();
                var root = CreateTemplateHost(result, host);

                Trace.WriteLine("Locating metadata section expander for keyboard focus validation.");
                var metadata = FindDescendant<System.Windows.Controls.Expander>(root, static expander => string.Equals(expander.Name, "MetadataSection", StringComparison.Ordinal));
                Assert.NotNull(metadata);
                Assert.True(metadata!.IsExpanded);
                Trace.WriteLine($"Metadata header content: {metadata.Header}");
                Assert.Equal("METADATA", metadata.Header);


                metadata.ApplyTemplate();
                var headerToggle = metadata.Template.FindName("HeaderToggle", metadata) as System.Windows.Controls.Primitives.ToggleButton;
                Trace.WriteLine($"Header toggle located: {headerToggle is not null}; Focusable={headerToggle?.Focusable}; IsTabStop={headerToggle?.IsTabStop}.");
                Assert.NotNull(headerToggle);
                Assert.True(headerToggle!.IsTabStop);
                Assert.True(headerToggle.Focusable);

                headerToggle.ApplyTemplate();
                var headerBorder = headerToggle.Template.FindName("HeaderBorder", headerToggle) as System.Windows.Controls.Border;
                var headerBorderBrush = headerBorder?.Background as System.Windows.Media.SolidColorBrush;
                Trace.WriteLine($"Header border background color: {headerBorderBrush?.Color}");
                Assert.NotNull(headerBorderBrush);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0xF3, 0xF4, 0xF6), headerBorderBrush!.Color);

                var headerForeground = headerToggle.Foreground as System.Windows.Media.SolidColorBrush;
                Trace.WriteLine($"Header toggle foreground color: {headerForeground?.Color}");
                Assert.NotNull(headerForeground);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80), headerForeground!.Color);


                metadata.IsExpanded = false;
                metadata.UpdateLayout();
                System.Windows.Input.Keyboard.Focus(headerToggle);
                Trace.WriteLine($"Header toggle keyboard focus state after collapse: {headerToggle.IsKeyboardFocused}");
                Assert.True(headerToggle.IsKeyboardFocused);


                var links = FindDescendant<System.Windows.Controls.ItemsControl>(root, static control => string.Equals(control.Name, "LinksItemsControl", StringComparison.Ordinal));
                Assert.NotNull(links);
                Assert.True(links!.HasItems);

                var attachments = FindDescendant<System.Windows.Controls.ItemsControl>(root, static control => string.Equals(control.Name, "AttachmentsItemsControl", StringComparison.Ordinal));
                Assert.NotNull(attachments);
                Assert.True(attachments!.HasItems);

                var relations = FindDescendant<System.Windows.Controls.ItemsControl>(root, static control => string.Equals(control.Name, "RelationsItemsControl", StringComparison.Ordinal));
                Assert.NotNull(relations);
                Assert.True(relations!.HasItems);

                var notesPlaceholder = FindDescendant<System.Windows.Controls.TextBlock>(root, static text => string.Equals(text.Name, "NotesPlaceholder", StringComparison.Ordinal));
                Assert.NotNull(notesPlaceholder);
                Assert.Equal(System.Windows.Visibility.Collapsed, notesPlaceholder!.Visibility);
            });
        }

        [Fact]
        public async Task Template_WithEmptyData_ShowsPlaceholders()
        {
            var entry = new Entry
            {
                Title = "Empty Entry"
            };
            var result = new LibrarySearchResult(entry, null, null);
            var host = new StubLibraryDetailViewModel(includeData: false);

            await RunOnStaThreadAsync(() =>
            {
                EnsureApplication();
                var root = CreateTemplateHost(result, host);

                Trace.WriteLine("Validating placeholder visibility for empty entry detail template.");

                AssertVisibility(root, "SourcePlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "AbstractPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "NotesPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "UserNotesPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "IdentifiersPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "LinksPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "AttachmentsPlaceholder", System.Windows.Visibility.Visible);
                AssertVisibility(root, "RelationsPlaceholder", System.Windows.Visibility.Visible);
            });
        }

        private static void AssertVisibility(System.Windows.DependencyObject root, string elementName, System.Windows.Visibility expected)
        {
            Trace.WriteLine($"Searching for element '{elementName}' to validate visibility state.");
            var textBlock = FindDescendant<System.Windows.Controls.TextBlock>(root, element => string.Equals(element.Name, elementName, StringComparison.Ordinal));
            Assert.NotNull(textBlock);
            Assert.Equal(expected, textBlock!.Visibility);
        }

        private static Entry CreatePopulatedEntry()
        {
            var entry = new Entry
            {
                Title = "Evidence-Based Medicine",
                DisplayName = "Evidence-Based Medicine",
                Type = EntryType.Publication,
                Year = 2024,
                Source = "Journal of Clinical Practice",
                Notes = "Notes for reviewers.",
                UserNotes = "Personal annotation.",
                InternalId = "KW-001",
                Doi = "10.1000/example",
                Pmid = "12345678",
                Nct = "NCT00000000",
                IsInternal = true
            };

            entry.Attachments.Add(new Attachment
            {
                Title = "Primary PDF",
                RelativePath = "attachments/primary.pdf",
                Kind = AttachmentKind.Supplement,
                AddedBy = string.IsNullOrWhiteSpace(Environment.UserName) ? "tester" : Environment.UserName,
                AddedUtc = DateTime.UtcNow,
                Notes = "Full text copy.",
                Tags = new List<string> { "pdf", "final" }
            });

            entry.Relations.Add(new Relation
            {
                Type = "related_to",
                TargetEntryId = "entry-2"
            });

            return entry;
        }

        private static System.Windows.Controls.ScrollViewer CreateTemplateHost(LibrarySearchResult result, StubLibraryDetailViewModel host)
        {
            var uri = new Uri("/LM.App.Wpf;component/Views/Library/LibraryEntryDetailTemplate.xaml", UriKind.Relative);
            var dictionary = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(uri);
            var template = (System.Windows.DataTemplate)dictionary["LibraryEntryDetailTemplate"];

            var presenter = new System.Windows.Controls.ContentPresenter
            {
                Content = result,
                ContentTemplate = template
            };

            var viewer = new System.Windows.Controls.ScrollViewer
            {
                Content = presenter,
                DataContext = host
            };

            InitializeElement(viewer);
            return viewer;
        }

        private static void InitializeElement(System.Windows.FrameworkElement element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.Measure(new System.Windows.Size(800, 600));
            element.Arrange(new System.Windows.Rect(0, 0, 800, 600));
            element.UpdateLayout();
        }

        private static void EnsureApplication()
        {
            if (System.Windows.Application.Current is null)
            {
                _ = new System.Windows.Application();
            }
        }

        private static T? FindDescendant<T>(System.Windows.DependencyObject root, Func<T, bool>? predicate = null)
            where T : class
        {
            if (root is null)
            {
                return null;
            }

            var queue = new Queue<System.Windows.DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                if (next is T candidate && (predicate is null || predicate(candidate)))
                {
                    return candidate;
                }

                var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(next);
                for (var i = 0; i < count; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(next, i);
                    if (child is not null)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return null;
        }

        private static Task RunOnStaThreadAsync(Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    completion.SetResult(true);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return completion.Task;
        }

        private sealed class StubLibraryDetailViewModel
        {
            public StubLibraryDetailViewModel(bool includeData)
            {
                Results = new StubResults(includeData);
                EditEntryCommand = new StubCommand();
                OpenAttachmentCommand = new StubCommand();
                OpenLinkCommand = new StubCommand();
            }

            public StubResults Results { get; }

            public System.Windows.Input.ICommand EditEntryCommand { get; }

            public System.Windows.Input.ICommand OpenAttachmentCommand { get; }

            public System.Windows.Input.ICommand OpenLinkCommand { get; }
        }

        private sealed class StubResults
        {
            public StubResults(bool includeData)
            {
                LinkItems = new ObservableCollection<LibraryLinkItem>();

                if (includeData)
                {
                    SelectedAbstract = "Abstract content.";
                    HasSelectedAbstract = true;
                    var link = new LibraryLinkItem("Example", "https://example.com", LinkItemKind.Url);
                    LinkItems.Add(link);
                    HasLinkItems = true;
                }
                else
                {
                    SelectedAbstract = null;
                    HasSelectedAbstract = false;
                    HasLinkItems = false;
                }
            }

            public ObservableCollection<LibraryLinkItem> LinkItems { get; }

            public string? SelectedAbstract { get; }

            public bool HasSelectedAbstract { get; }

            public bool HasLinkItems { get; }
        }

        private sealed class StubCommand : System.Windows.Input.ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
            }
        }
    }
}
