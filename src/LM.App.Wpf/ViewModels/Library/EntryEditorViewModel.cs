#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using LM.App.Wpf.Common.Dialogs;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Models;
using LM.Infrastructure.Hooks;
using LM.Infrastructure.Utils;

namespace LM.App.Wpf.ViewModels.Library
{
    internal sealed partial class EntryEditorViewModel : DialogViewModelBase
    {
        private readonly IEntryStore _store;
        private readonly HookOrchestrator _orchestrator;
        private readonly IWorkSpaceService _workspace;

        private Entry? _entry;
        private string? _articleHookJson;
        private string? _originalAuthorsSignature;

        public EntryEditorViewModel(IEntryStore store,
                                    HookOrchestrator orchestrator,
                                    IWorkSpaceService workspace)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            EntryTypes = Enum.GetValues(typeof(EntryType));
        }

        public EntryEditorItem? Item { get; private set; }

        public Array EntryTypes { get; }

        public bool WasSaved { get; private set; }

        public async Task<bool> LoadAsync(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("Entry id must not be empty.", nameof(entryId));

            var entry = await _store.GetByIdAsync(entryId).ConfigureAwait(true);
            if (entry is null)
            {
                System.Windows.MessageBox.Show(
                    "The selected entry could not be found.",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }

            _entry = CloneEntry(entry);
            WasSaved = false;

            _articleHookJson = await TryLoadArticleHookJsonAsync(entryId).ConfigureAwait(true);
            _originalAuthorsSignature = NormalizeAuthorsSignature(_entry.Authors);

            Item = CreateItem(_entry);
            return true;
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose(false);
        }

        [RelayCommand(CanExecute = nameof(CanGenerateShortTitle))]
        private void GenerateShortTitle()
        {
            if (Item is null)
                return;

            var authors = SplitList(Item.AuthorsCsv);
            Item.DisplayName = BibliographyHelper.GenerateShortTitle(
                Item.Title,
                authors,
                Item.Source,
                Item.Year);
        }

        private bool CanGenerateShortTitle() => Item is not null;

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (_entry is null || Item is null)
                return;

            try
            {
                ApplyEdits(_entry, Item);

                await _store.SaveAsync(_entry).ConfigureAwait(true);

                await UpdateArticleHookAsync(_entry).ConfigureAwait(true);

                _originalAuthorsSignature = NormalizeAuthorsSignature(_entry.Authors);
                WasSaved = true;
                RequestClose(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save entry:{Environment.NewLine}{ex.Message}",
                    "Edit Entry",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static EntryEditorItem CreateItem(Entry entry)
        {
            var authorsCsv = ToCsv(entry.Authors);
            var tagsCsv = ToCsv(entry.Tags);
            var originalFile = !string.IsNullOrWhiteSpace(entry.OriginalFileName)
                ? entry.OriginalFileName
                : (string.IsNullOrWhiteSpace(entry.MainFilePath) ? null : Path.GetFileName(entry.MainFilePath));

            return new EntryEditorItem
            {
                Type = entry.Type,
                Title = string.IsNullOrWhiteSpace(entry.Title) ? null : entry.Title,
                DisplayName = entry.DisplayName,
                AuthorsCsv = authorsCsv,
                Year = entry.Year,
                Source = entry.Source,
                TagsCsv = tagsCsv,
                IsInternal = entry.IsInternal,
                Doi = entry.Doi,
                Pmid = entry.Pmid,
                InternalId = entry.InternalId,
                Notes = entry.UserNotes,
                OriginalFileName = originalFile
            };
        }

        private static Entry CloneEntry(Entry source)
        {
            return new Entry
            {
                Id = source.Id,
                Title = source.Title,
                DisplayName = source.DisplayName,
                ShortTitle = source.ShortTitle,
                Type = source.Type,
                Year = source.Year,
                Source = source.Source,
                Authors = source.Authors?.ToList() ?? new List<string>(),
                AddedBy = source.AddedBy,
                AddedOnUtc = source.AddedOnUtc,
                InternalId = source.InternalId,
                Doi = source.Doi,
                Pmid = source.Pmid,
                Nct = source.Nct,
                Links = source.Links?.ToList() ?? new List<string>(),
                IsInternal = source.IsInternal,
                Tags = source.Tags?.ToList() ?? new List<string>(),
                MainFilePath = source.MainFilePath,
                MainFileHashSha256 = source.MainFileHashSha256,
                OriginalFileName = source.OriginalFileName,
                Version = source.Version,
                Notes = source.Notes,
                UserNotes = source.UserNotes,
                Attachments = source.Attachments?.Select(a => new Attachment
                {
                    Id = a.Id,
                    RelativePath = a.RelativePath,
                    Notes = a.Notes,
                    Tags = a.Tags?.ToList() ?? new List<string>(),
                    Title = a.Title,
                    Kind = a.Kind,
                    AddedBy = a.AddedBy,
                    AddedUtc = a.AddedUtc
                }).ToList() ?? new List<Attachment>(),
                Relations = source.Relations?.Select(r => new Relation
                {
                    Type = r.Type,
                    TargetEntryId = r.TargetEntryId
                }).ToList() ?? new List<Relation>()
            };
        }

        private void ApplyEdits(Entry entry, EntryEditorItem model)
        {
            entry.Type = model.Type;
            entry.Title = model.Title?.Trim() ?? string.Empty;
            entry.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName!.Trim();
            entry.Authors = SplitList(model.AuthorsCsv);
            entry.Year = model.Year;
            entry.Source = TrimOrNull(model.Source);
            entry.Doi = TrimOrNull(model.Doi);
            entry.Pmid = TrimOrNull(model.Pmid);
            entry.InternalId = TrimOrNull(model.InternalId);
            entry.Tags = SplitList(model.TagsCsv, distinct: true);
            entry.IsInternal = model.IsInternal;
            entry.UserNotes = TrimOrNull(model.Notes);

            if (entry.Type != EntryType.Publication)
                _articleHookJson = null;
        }

        private async Task UpdateArticleHookAsync(Entry entry)
        {
            if (entry.Type != EntryType.Publication)
                return;

            var json = _articleHookJson;
            if (string.IsNullOrWhiteSpace(json))
            {
                json = await TryLoadArticleHookJsonAsync(entry.Id).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EntryEditorViewModel] Failed to parse article hook for '{entry.Id}': {ex}");
                return;
            }

            if (root is null)
                return;

            if (root["article"] is JsonObject articleObj)
                articleObj["title"] = entry.Title ?? entry.DisplayName ?? string.Empty;

            if (root["journal"] is JsonObject journalObj)
            {
                journalObj["title"] = entry.Source ?? string.Empty;

                if (entry.Year.HasValue)
                {
                    var issueObj = journalObj["issue"] as JsonObject ?? new JsonObject();
                    issueObj["pubDate"] = new JsonObject
                    {
                        ["year"] = entry.Year.Value
                    };
                    journalObj["issue"] = issueObj;
                }
            }

            if (root["identifier"] is JsonObject identifierObj)
            {
                identifierObj["doi"] = string.IsNullOrWhiteSpace(entry.Doi) ? null : entry.Doi;
                identifierObj["pmid"] = entry.Pmid ?? string.Empty;
            }

            if (AuthorsChanged(entry.Authors))
            {
                var authorsArray = new JsonArray();
                foreach (var author in entry.Authors ?? new List<string>())
                {
                    var trimmed = author?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    var parts = trimmed.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        authorsArray.Add(new JsonObject
                        {
                            ["lastName"] = parts[0],
                            ["foreName"] = parts[1]
                        });
                    }
                    else
                    {
                        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var last = tokens.Length > 0 ? tokens[^1] : trimmed;
                        authorsArray.Add(new JsonObject
                        {
                            ["lastName"] = last,
                            ["foreName"] = trimmed
                        });
                    }
                }
                root["authors"] = authorsArray;
            }

            try
            {
                var updatedHook = root.Deserialize<ArticleHook>(JsonStd.Options);
                if (updatedHook is null)
                    return;

                await _orchestrator.ProcessAsync(
                    entry.Id,
                    new HookContext { Article = updatedHook },
                    System.Threading.CancellationToken.None).ConfigureAwait(false);

                _articleHookJson = root.ToJsonString(JsonStd.Options);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EntryEditorViewModel] Failed to persist article hook for '{entry.Id}': {ex}");
            }
        }

        private static string? TrimOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string? ToCsv(IEnumerable<string>? values)
        {
            if (values is null)
                return null;
            var parts = values
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();
            return parts.Length == 0 ? null : string.Join(", ", parts);
        }

        private static List<string> SplitList(string? csv, bool distinct = false)
        {
            var parts = (csv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);

            return (distinct ? parts.Distinct(StringComparer.OrdinalIgnoreCase) : parts).ToList();
        }

        private bool AuthorsChanged(IReadOnlyCollection<string>? authors)
        {
            var signature = NormalizeAuthorsSignature(authors);
            return !string.Equals(signature, _originalAuthorsSignature, StringComparison.Ordinal);
        }

        private static string NormalizeAuthorsSignature(IEnumerable<string>? authors)
        {
            if (authors is null)
                return string.Empty;

            return string.Join("|", authors
                .Select(a => a?.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a!.ToUpperInvariant()));
        }

        private async Task<string?> TryLoadArticleHookJsonAsync(string entryId)
        {
            try
            {
                var relative = Path.Combine("entries", entryId, "hooks", "article.json");
                var absolute = _workspace.GetAbsolutePath(relative);
                if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
                    return null;

                return await File.ReadAllTextAsync(absolute).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EntryEditorViewModel] Failed to load article hook for '{entryId}': {ex}");
                return null;
            }
        }
    }
}
