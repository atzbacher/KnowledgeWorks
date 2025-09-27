using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LM.Infrastructure.Review.Dto;
using LM.Infrastructure.Review.Mappers;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    private async Task LoadAssignmentsAsync(string projectDir, string projectId, CancellationToken ct)
    {
        var assignmentDir = Path.Combine(projectDir, "assignments");
        if (!Directory.Exists(assignmentDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(assignmentDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var doc = await ReadJsonAsync<ScreeningAssignmentDto>(file, ct).ConfigureAwait(false);
            if (doc is null || string.IsNullOrWhiteSpace(doc.Id))
            {
                continue;
            }

            if (!string.Equals(doc.ProjectId, projectId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var assignment = ScreeningAssignmentMapper.ToDomain(doc);
                _assignmentDtos[assignment.Id] = doc;
                _assignments[assignment.Id] = assignment;
                if (!_assignmentIdsByStage.ContainsKey(assignment.StageId))
                {
                    _assignmentIdsByStage[assignment.StageId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                _assignmentIdsByStage[assignment.StageId].Add(assignment.Id);
            }
            catch
            {
                // Ignore malformed assignment files during initialization.
            }
        }
    }

    private async Task LoadStagesAsync(string projectDir, ReviewProject project, CancellationToken ct)
    {
        var stageDir = Path.Combine(projectDir, "stages");
        if (!Directory.Exists(stageDir))
        {
            return;
        }

        var definitions = project.StageDefinitions.ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(stageDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var doc = await ReadJsonAsync<ReviewStageDto>(file, ct).ConfigureAwait(false);
            if (doc is null || string.IsNullOrWhiteSpace(doc.Id))
            {
                continue;
            }

            if (!string.Equals(doc.ProjectId, project.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!definitions.TryGetValue(doc.DefinitionId, out var definition))
            {
                continue;
            }

            try
            {
                var stage = ReviewStageMapper.ToDomain(doc, definition, CollectAssignments(doc.Id));
                _stageDtos[doc.Id] = doc;
                _stages[doc.Id] = stage;
                _stageIdsByProject.TryAdd(project.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                _stageIdsByProject[project.Id].Add(doc.Id);
            }
            catch
            {
                // Skip malformed stage files.
            }
        }
    }

    private async Task LoadFormResponsesAsync(string projectDir, string projectId, CancellationToken ct)
    {
        var formsDir = Path.Combine(projectDir, "forms");
        if (!Directory.Exists(formsDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(formsDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var doc = await ReadJsonAsync<FormResponseDocument>(file, ct).ConfigureAwait(false);
            if (doc is null || string.IsNullOrWhiteSpace(doc.Id))
            {
                continue;
            }

            if (!string.Equals(doc.ProjectId, projectId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var response = FormResponseMapper.ToDomain(doc, _jsonOptions);
                var key = FormKey(projectId, response.Id);
                _formDocs[key] = doc;
                _formResponses[key] = response;
                _formIdsByProject.TryAdd(projectId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                _formIdsByProject[projectId].Add(response.Id);
            }
            catch
            {
                // Ignore malformed form responses.
            }
        }
    }

    private IReadOnlyCollection<ScreeningAssignment> CollectAssignments(string stageId)
    {
        if (!_assignmentIdsByStage.TryGetValue(stageId, out var ids) || ids.Count == 0)
        {
            return Array.Empty<ScreeningAssignment>();
        }

        return ids.Select(id => _assignments[id]).ToList();
    }

    private void RebuildStage(string stageId)
    {
        if (!_stageDtos.TryGetValue(stageId, out var doc))
        {
            return;
        }

        if (!_projects.TryGetValue(doc.ProjectId, out var project))
        {
            return;
        }

        var definition = project.StageDefinitions.FirstOrDefault(d => string.Equals(d.Id, doc.DefinitionId, StringComparison.Ordinal));
        if (definition is null)
        {
            return;
        }

        var assignments = CollectAssignments(stageId);
        try
        {
            var stage = ReviewStageMapper.ToDomain(doc, definition, assignments);
            _stages[stageId] = stage;
        }
        catch
        {
            // ignore rebuild failures; stage will be refreshed on next initialize
        }
    }

    private async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct).ConfigureAwait(false);
        }
        catch
        {
            return default;
        }
    }

    private async Task WriteJsonAsync<T>(string path, T payload, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var lockPath = path + ".lock";
        var tmpPath = path + ".tmp";

        var lockHandle = AcquireLock(lockPath);
        try
        {
            await using var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, payload, _jsonOptions, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            await ReplaceFileWithRetryAsync(tmpPath, path, ct).ConfigureAwait(false);
        }
        finally
        {
            await lockHandle.DisposeAsync().ConfigureAwait(false);

            try
            {
                File.Delete(lockPath);
            }
            catch
            {
                // Ignore lock cleanup issues.
            }

            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
    }

    private async Task ReplaceFileWithRetryAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        var waitStart = DateTime.UtcNow;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (DateTime.UtcNow - waitStart < _lockTimeout)
            {
                await Task.Delay(_ioRetryDelay, ct).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (DateTime.UtcNow - waitStart < _lockTimeout)
            {
                await Task.Delay(_ioRetryDelay, ct).ConfigureAwait(false);
            }
        }
    }

    private FileStream AcquireLock(string lockPath)
    {
        var waitStart = DateTime.UtcNow;

        while (true)
        {
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                stream.SetLength(0);
                return stream;
            }
            catch (IOException) when (File.Exists(lockPath))
            {
                var lastWrite = File.GetLastWriteTimeUtc(lockPath);
                if (lastWrite == DateTime.MinValue || DateTime.UtcNow - lastWrite > _lockTimeout)
                {
                    try
                    {
                        File.Delete(lockPath);
                        continue;
                    }
                    catch (IOException)
                    {
                        // lock file is still busy; fall through to retry loop.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // lock file is still busy; fall through to retry loop.
                    }
                }

                if (DateTime.UtcNow - waitStart >= _lockTimeout)
                {
                    throw;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(200));
            }
        }
    }

    private string GetProjectPath(string projectId) => Path.Combine(_reviewsRoot, projectId, "project.json");
    private string GetStagePath(string projectId, string stageId) => Path.Combine(_reviewsRoot, projectId, "stages", stageId + ".json");
    private string GetAssignmentPath(string projectId, string assignmentId) => Path.Combine(_reviewsRoot, projectId, "assignments", assignmentId + ".json");
    private string GetFormResponsePath(string projectId, string responseId) => Path.Combine(_reviewsRoot, projectId, "forms", responseId + ".json");

    private static string FormKey(string projectId, string responseId) => string.Concat(projectId, "::", responseId);

    private void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(_reviewsRoot))
        {
            throw new InvalidOperationException("Store must be initialized before use.");
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
