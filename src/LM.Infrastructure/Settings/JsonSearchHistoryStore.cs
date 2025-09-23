using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;
using LM.Core.Models.Search;

namespace LM.Infrastructure.Settings
{
    /// <summary>Persists search history under the active workspace.</summary>
    public sealed class JsonSearchHistoryStore : ISearchHistoryStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private readonly IWorkSpaceService _workspace;

        public JsonSearchHistoryStore(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<SearchHistoryDocument> LoadAsync(CancellationToken ct = default)
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return new SearchHistoryDocument();
            }

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var payload = await JsonSerializer.DeserializeAsync<HistoryPayload>(stream, Options, ct).ConfigureAwait(false);
                if (payload?.Entries is null)
                    return new SearchHistoryDocument();

                var entries = payload.Entries.Select(e => new SearchHistoryEntry
                {
                    Query = e.Query ?? string.Empty,
                    Database = e.Database,
                    From = e.From,
                    To = e.To,
                    ExecutedUtc = e.ExecutedUtc
                }).Where(e => !string.IsNullOrWhiteSpace(e.Query)).ToArray();

                return new SearchHistoryDocument { Entries = entries };
            }
            catch (JsonException)
            {
                return new SearchHistoryDocument();
            }
        }

        public async Task SaveAsync(SearchHistoryDocument document, CancellationToken ct = default)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));

            var payload = new HistoryPayload
            {
                Entries = document.Entries?.Select(e => new HistoryEntry
                {
                    Query = e.Query,
                    Database = e.Database,
                    From = e.From,
                    To = e.To,
                    ExecutedUtc = e.ExecutedUtc
                }).ToArray()
            };

            var path = GetFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, payload, Options, ct).ConfigureAwait(false);
        }

        private string GetFilePath()
        {
            var root = _workspace.GetWorkspaceRoot();
            var directory = Path.Combine(root, ".kw");
            return Path.Combine(directory, "search-history.json");
        }

        private sealed class HistoryPayload
        {
            public HistoryEntry[]? Entries { get; set; }
        }

        private sealed class HistoryEntry
        {
            public string? Query { get; set; }
            public SearchDatabase Database { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public DateTimeOffset ExecutedUtc { get; set; }
        }
    }
}
