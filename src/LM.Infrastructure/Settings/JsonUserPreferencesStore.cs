using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions.Configuration;
using LM.Core.Models;

namespace LM.Infrastructure.Settings
{
    /// <summary>Persists user-level preferences under %LOCALAPPDATA%\KnowledgeWorks.</summary>
    public sealed class JsonUserPreferencesStore : IUserPreferencesStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private readonly string _filePath;

        public JsonUserPreferencesStore()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(baseDir, "KnowledgeWorks");
            _filePath = Path.Combine(directory, "user-preferences.json");
        }

        public async Task<UserPreferences> LoadAsync(CancellationToken ct = default)
        {
            if (!File.Exists(_filePath))
            {
                return new UserPreferences();
            }

            try
            {
                await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var preferences = await JsonSerializer.DeserializeAsync<UserPreferences>(stream, Options, ct).ConfigureAwait(false);
                return preferences ?? new UserPreferences();
            }
            catch (JsonException)
            {
                return new UserPreferences();
            }
        }

        public async Task SaveAsync(UserPreferences preferences, CancellationToken ct = default)
        {
            if (preferences is null)
                throw new ArgumentNullException(nameof(preferences));

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, preferences, Options, ct).ConfigureAwait(false);
        }
    }
}
