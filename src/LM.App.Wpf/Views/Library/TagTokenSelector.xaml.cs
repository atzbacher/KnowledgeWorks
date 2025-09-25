#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace LM.App.Wpf.Views.Library
{
    public partial class TagTokenSelector : System.Windows.Controls.UserControl
    {
        internal static readonly System.Windows.DependencyProperty SelectedTagsProperty =
            System.Windows.DependencyProperty.Register(
                nameof(SelectedTags),
                typeof(ObservableCollection<string>),
                typeof(TagTokenSelector),
                new System.Windows.PropertyMetadata(null, OnSelectedTagsChanged));

        internal static readonly System.Windows.DependencyProperty TagVocabularyProperty =
            System.Windows.DependencyProperty.Register(
                nameof(TagVocabulary),
                typeof(IEnumerable<string>),
                typeof(TagTokenSelector),
                new System.Windows.PropertyMetadata(Array.Empty<string>(), OnTagVocabularyChanged));

        private readonly ObservableCollection<string> _filteredSuggestions = new();
        private readonly HashSet<string> _selectedTagSet = new(StringComparer.OrdinalIgnoreCase);
        private string _currentInput = string.Empty;

        public TagTokenSelector()
        {
            InitializeComponent();
        }

        public ObservableCollection<string>? SelectedTags
        {
            get => (ObservableCollection<string>?)GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }

        public IEnumerable<string> TagVocabulary
        {
            get => (IEnumerable<string>)GetValue(TagVocabularyProperty);
            set => SetValue(TagVocabularyProperty, value);
        }

        public ObservableCollection<string> FilteredSuggestions => _filteredSuggestions;

        private static void OnSelectedTagsChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var control = (TagTokenSelector)d;
            control.HandleSelectedTagsChanged(e.OldValue as ObservableCollection<string>, e.NewValue as ObservableCollection<string>);
        }

        private static void OnTagVocabularyChanged(System.Windows.DependencyObject d, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var control = (TagTokenSelector)d;
            control.UpdateSuggestions(control._currentInput);
        }

        private void HandleSelectedTagsChanged(ObservableCollection<string>? oldValue, ObservableCollection<string>? newValue)
        {
            if (oldValue is not null)
            {
                oldValue.CollectionChanged -= OnSelectedTagsCollectionChanged;
            }

            if (newValue is null)
            {
                newValue = new ObservableCollection<string>();
                SetValue(SelectedTagsProperty, newValue);
                return;
            }

            newValue.CollectionChanged += OnSelectedTagsCollectionChanged;

            RebuildSelectedSet();
            UpdateSuggestions(_currentInput);
        }

        private void OnSelectedTagsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildSelectedSet();
            UpdateSuggestions(_currentInput);
        }

        private void RebuildSelectedSet()
        {
            _selectedTagSet.Clear();
            if (SelectedTags is null)
            {
                return;
            }

            foreach (var tag in SelectedTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var trimmed = tag.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                _selectedTagSet.Add(trimmed);
            }
        }

        private void OnInputTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _currentInput = InputBox.Text;
            UpdateSuggestions(_currentInput);
        }

        private void OnInputPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (!TryAcceptHighlightedSuggestion())
                {
                    CommitInputText();
                }

                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.OemComma)
            {
                CommitInputText();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Back && string.IsNullOrWhiteSpace(InputBox.Text))
            {
                RemoveLastTag();
                e.Handled = true;
            }
        }

        private void OnSuggestionClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is string tag)
            {
                if (AddTag(tag))
                {
                    ClearInput();
                    e.Handled = true;
                }

                listBox.SelectedItem = null;
            }
        }

        private void OnSuggestionKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is string tag)
            {
                if (AddTag(tag))
                {
                    ClearInput();
                }

                e.Handled = true;
            }
        }

        private void OnRemoveTagClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.CommandParameter is string tag)
            {
                RemoveTag(tag);
            }
        }

        private void CommitInputText()
        {
            if (AddTag(_currentInput))
            {
                ClearInput();
            }
        }

        private bool TryAcceptHighlightedSuggestion()
        {
            if (SuggestionList.SelectedItem is string tag && AddTag(tag))
            {
                ClearInput();
                return true;
            }

            return TryAcceptFirstSuggestion();
        }

        private bool TryAcceptFirstSuggestion()
        {
            var first = _filteredSuggestions.FirstOrDefault();
            if (first is null)
            {
                return false;
            }

            if (AddTag(first))
            {
                ClearInput();
                return true;
            }

            return false;
        }

        private bool AddTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var trimmed = tag.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (_selectedTagSet.Contains(trimmed))
            {
                return false;
            }

            SelectedTags ??= new ObservableCollection<string>();
            SelectedTags.Add(trimmed);
            _selectedTagSet.Add(trimmed);
            return true;
        }

        private void RemoveTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || SelectedTags is null)
            {
                return;
            }

            var trimmed = tag.Trim();
            for (var i = SelectedTags.Count - 1; i >= 0; i--)
            {
                if (string.Equals(SelectedTags[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedTags.RemoveAt(i);
                    break;
                }
            }

            _selectedTagSet.Remove(trimmed);
            UpdateSuggestions(_currentInput);
        }

        private void RemoveLastTag()
        {
            if (SelectedTags is null || SelectedTags.Count == 0)
            {
                return;
            }

            var last = SelectedTags[^1];
            SelectedTags.RemoveAt(SelectedTags.Count - 1);
            _selectedTagSet.Remove(last);
            UpdateSuggestions(_currentInput);
        }

        private void ClearInput()
        {
            _currentInput = string.Empty;
            InputBox.Text = string.Empty;
            UpdateSuggestions(_currentInput);
            InputBox.Focus();
        }

        private void UpdateSuggestions(string? filter)
        {
            var vocabulary = TagVocabulary ?? Array.Empty<string>();
            var trimmed = (filter ?? string.Empty).Trim();

            var candidates = vocabulary
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .Select(static tag => tag.Trim())
                .Where(tag => tag.Length > 0 && !_selectedTagSet.Contains(tag));

            if (!string.IsNullOrEmpty(trimmed))
            {
                candidates = candidates.Where(tag => tag.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var ordered = candidates
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();

            _filteredSuggestions.Clear();
            foreach (var suggestion in ordered)
            {
                _filteredSuggestions.Add(suggestion);
            }
        }
    }
}
