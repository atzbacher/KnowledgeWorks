using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models.Filters;

namespace LM.App.Wpf.Library
{
    /// <summary>
    /// Persists named Library filter presets under the active workspace.
    /// </summary>
    public sealed class LibraryFilterPresetStore
    {
        private readonly IWorkSpaceService _workspace;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };

        public LibraryFilterPresetStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task SavePresetAsync(LibraryFilterPreset preset, CancellationToken ct = default)
        {
            if (preset is null)
                throw new ArgumentNullException(nameof(preset));

            var file = await LoadAsync(ct).ConfigureAwait(false);

            var existingIndex = file.Presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            preset.SavedUtc = DateTime.UtcNow;

            if (existingIndex >= 0)
            {
                file.Presets[existingIndex] = preset;
            }
            else
            {
                file.Presets.Add(preset);
            }

            file.Presets.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            await SaveAsync(file, ct).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<LibraryFilterPreset>> ListPresetsAsync(CancellationToken ct = default)
        {
            var file = await LoadAsync(ct).ConfigureAwait(false);
            return file.Presets
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Clone())
                .ToArray();
        }

        public async Task<LibraryFilterPreset?> TryGetPresetAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var file = await LoadAsync(ct).ConfigureAwait(false);
            var preset = file.Presets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            return preset?.Clone();
        }

        public async Task DeletePresetAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var file = await LoadAsync(ct).ConfigureAwait(false);
            file.Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            await SaveAsync(file, ct).ConfigureAwait(false);
        }

        private async Task<LibraryPresetFile> LoadAsync(CancellationToken ct)
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new LibraryPresetFile();

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            var file = await JsonSerializer.DeserializeAsync<LibraryPresetFile>(stream, JsonOptions, ct).ConfigureAwait(false);
            return file ?? new LibraryPresetFile();
        }

        private async Task SaveAsync(LibraryPresetFile file, CancellationToken ct)
        {
            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(file, JsonOptions);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }

        private string GetFilePath()
        {
            var root = _workspace.GetWorkspaceRoot();
            return Path.Combine(root, "library", "filter-presets.json");
        }

        private sealed class LibraryPresetFile
        {
            public List<LibraryFilterPreset> Presets { get; set; } = new();
        }
    }

    public sealed class LibraryFilterPreset
    {
        public string Name { get; set; } = string.Empty;
        public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
        public LibraryFilterState State { get; set; } = new();

        internal LibraryFilterPreset Clone()
            => new()
            {
                Name = Name,
                SavedUtc = SavedUtc,
                State = State.Clone()
            };
    }

    public sealed class LibraryFilterState
    {
        public bool UseFullTextSearch { get; set; }
        public string? FullTextQuery { get; set; }
        public bool FullTextInTitle { get; set; } = true;
        public bool FullTextInAbstract { get; set; } = true;
        public bool FullTextInContent { get; set; } = true;
        public string? TitleContains { get; set; }
        public string? AuthorContains { get; set; }
        public List<string> Tags { get; set; } = new();
        public TagMatchMode TagMatchMode { get; set; } = TagMatchMode.Any;
        public bool? IsInternal { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? SourceContains { get; set; }
        public string? InternalIdContains { get; set; }
        public string? DoiContains { get; set; }
        public string? PmidContains { get; set; }
        public string? NctContains { get; set; }
        public string? AddedByContains { get; set; }
        public DateTime? AddedOnFrom { get; set; }
        public DateTime? AddedOnTo { get; set; }
        public bool TypePublication { get; set; } = true;
        public bool TypePresentation { get; set; } = true;
        public bool TypeWhitePaper { get; set; } = true;
        public bool TypeSlideDeck { get; set; } = true;
        public bool TypeReport { get; set; } = true;
        public bool TypeOther { get; set; } = true;

        internal LibraryFilterState Clone()
        {
            var clone = (LibraryFilterState)MemberwiseClone();
            clone.Tags = Tags is null ? new List<string>() : new List<string>(Tags);
            return clone;
        }
    }
}
