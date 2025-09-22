using System;

using System.IO;
using System.Linq;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.HubSpoke.Extraction;
using LM.HubSpoke.FileSystem;
using Microsoft.Data.Sqlite;

namespace LM.Infrastructure.Extraction
{
    public sealed partial class SqliteExtractionRepository : IExtractionRepository

    {
        private const int SessionRetentionLimit = 200;

        private readonly IWorkSpaceService _workspace;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private string? _dbPath;
        private volatile bool _initialized;

        public SqliteExtractionRepository(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }


        private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized) return;

                var dbPath = _workspace.GetLocalDbPath();
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await ExtractionSchemaBootstrapper.EnsureAsync(_workspace, cancellationToken);

                _dbPath = dbPath;
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private SqliteConnection CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(_dbPath))
                throw new InvalidOperationException("Repository has not been initialized.");

            return new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;");
        }


        private async Task PersistDescriptorAsync(RegionDescriptor descriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = WorkspaceLayout.ExtractionDescriptorPath(_workspace, descriptor.RegionHash);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = RegionDescriptorMapper.SerializeDescriptor(descriptor);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }

        private void DeleteDescriptorArtifacts(RegionDescriptor descriptor)
        {
            var descriptorPath = WorkspaceLayout.ExtractionDescriptorPath(_workspace, descriptor.RegionHash);
            DeleteIfExists(descriptorPath);
            DeleteIfExists(descriptor.ImagePath);
            DeleteIfExists(descriptor.OcrTextPath);
            DeleteIfExists(descriptor.OfficePackagePath);
        }

        private void DeleteIfExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            string absolute = Path.IsPathRooted(path)
                ? path
                : _workspace.GetAbsolutePath(path);

            if (File.Exists(absolute))
            {
                try { File.Delete(absolute); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string BuildMatchExpression(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            var tokens = query
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeToken)
                .Where(t => t.Length > 0)
                .Select(t => t + "*");

            return string.Join(" AND ", tokens);
        }

        private static string SanitizeToken(string token)
        {
            var builder = new StringBuilder(token.Length);
            foreach (var ch in token)
            {
                if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                    builder.Append(char.ToLowerInvariant(ch));
            }
            return builder.ToString();
        }
    }
}
