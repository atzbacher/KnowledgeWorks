using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Infrastructure.Review.Dto;
using LM.Infrastructure.Review.Mappers;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    private readonly IWorkSpaceService _workspace;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _ioRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly Dictionary<string, ReviewProject> _projects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReviewStageDto> _stageDtos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReviewStage> _stages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScreeningAssignmentDto> _assignmentDtos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScreeningAssignment> _assignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FormResponseDocument> _formDocs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FormResponse> _formResponses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _stageIdsByProject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _assignmentIdsByStage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _formIdsByProject = new(StringComparer.OrdinalIgnoreCase);

    private string _reviewsRoot = string.Empty;

    internal JsonReviewProjectStore(IWorkSpaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    internal async Task InitializeAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _reviewsRoot = Path.Combine(_workspace.GetWorkspaceRoot(), "reviews");
            Directory.CreateDirectory(_reviewsRoot);

            _projects.Clear();
            _stageDtos.Clear();
            _stages.Clear();
            _assignmentDtos.Clear();
            _assignments.Clear();
            _formDocs.Clear();
            _formResponses.Clear();
            _stageIdsByProject.Clear();
            _assignmentIdsByStage.Clear();
            _formIdsByProject.Clear();

            foreach (var projectDir in Directory.EnumerateDirectories(_reviewsRoot))
            {
                ct.ThrowIfCancellationRequested();

                var projectPath = Path.Combine(projectDir, "project.json");
                if (!File.Exists(projectPath))
                {
                    continue;
                }

                var projectDto = await ReadJsonAsync<ReviewProjectDto>(projectPath, ct).ConfigureAwait(false);
                if (projectDto is null || string.IsNullOrWhiteSpace(projectDto.Id))
                {
                    continue;
                }

                var project = ReviewProjectMapper.ToDomain(projectDto);
                _projects[project.Id] = project;
                _stageIdsByProject[project.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _formIdsByProject[project.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var (projectId, project) in _projects)
            {
                ct.ThrowIfCancellationRequested();
                var projectDir = Path.Combine(_reviewsRoot, projectId);

                await LoadAssignmentsAsync(projectDir, projectId, ct).ConfigureAwait(false);
                await LoadStagesAsync(projectDir, project, ct).ConfigureAwait(false);
                await LoadFormResponsesAsync(projectDir, projectId, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task SaveProjectAsync(ReviewProject project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var dto = ReviewProjectMapper.ToDto(project);
            var path = GetProjectPath(project.Id);
            await WriteJsonAsync(path, dto, ct).ConfigureAwait(false);
            _projects[project.Id] = project;
            _stageIdsByProject.TryAdd(project.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _formIdsByProject.TryAdd(project.Id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<ReviewProject?> GetProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _projects.TryGetValue(projectId, out var project) ? project : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<IReadOnlyList<ReviewProject>> GetProjectsAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _projects.Values.ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task DeleteProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var dir = Path.Combine(_reviewsRoot, projectId);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }

            if (_projects.Remove(projectId))
            {
                if (_stageIdsByProject.TryGetValue(projectId, out var stageIds))
                {
                    foreach (var stageId in stageIds)
                    {
                        _stageDtos.Remove(stageId);
                        _stages.Remove(stageId);
                        if (_assignmentIdsByStage.TryGetValue(stageId, out var assignmentIds))
                        {
                            foreach (var assignmentId in assignmentIds)
                            {
                                _assignmentDtos.Remove(assignmentId);
                                _assignments.Remove(assignmentId);
                            }
                            _assignmentIdsByStage.Remove(stageId);
                        }
                    }
                    _stageIdsByProject.Remove(projectId);
                }

                if (_formIdsByProject.TryGetValue(projectId, out var formIds))
                {
                    foreach (var formId in formIds)
                    {
                        _formDocs.Remove(FormKey(projectId, formId));
                        _formResponses.Remove(FormKey(projectId, formId));
                    }
                    _formIdsByProject.Remove(projectId);
                }
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task SaveStageAsync(ReviewStage stage, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_projects.ContainsKey(stage.ProjectId))
            {
                throw new InvalidOperationException($"Project '{stage.ProjectId}' must be saved before stages can be persisted.");
            }

            var dto = ReviewStageMapper.ToDto(stage);
            var path = GetStagePath(stage.ProjectId, stage.Id);
            await WriteJsonAsync(path, dto, ct).ConfigureAwait(false);

            _stageDtos[stage.Id] = dto;
            _stageIdsByProject.TryAdd(stage.ProjectId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _stageIdsByProject[stage.ProjectId].Add(stage.Id);
            _stages[stage.Id] = stage;
            if (!_assignmentIdsByStage.ContainsKey(stage.Id))
            {
                _assignmentIdsByStage[stage.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<ReviewStage?> GetStageAsync(string stageId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _stages.TryGetValue(stageId, out var stage) ? stage : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<IReadOnlyList<ReviewStage>> GetStagesByProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_stageIdsByProject.TryGetValue(projectId, out var ids))
            {
                return Array.Empty<ReviewStage>();
            }

            return ids.Select(id => _stages[id]).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task DeleteStageAsync(string projectId, string stageId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var path = GetStagePath(projectId, stageId);
            DeleteFileIfExists(path);

            if (_stageDtos.Remove(stageId))
            {
                _stages.Remove(stageId);
            }

            if (_stageIdsByProject.TryGetValue(projectId, out var ids))
            {
                ids.Remove(stageId);
                if (ids.Count == 0)
                {
                    _stageIdsByProject.Remove(projectId);
                }
            }

            if (_assignmentIdsByStage.TryGetValue(stageId, out var assignmentIds))
            {
                foreach (var assignmentId in assignmentIds)
                {
                    var assignmentPath = GetAssignmentPath(projectId, assignmentId);
                    DeleteFileIfExists(assignmentPath);
                    _assignmentDtos.Remove(assignmentId);
                    _assignments.Remove(assignmentId);
                }

                _assignmentIdsByStage.Remove(stageId);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task SaveAssignmentAsync(string projectId, ScreeningAssignment assignment, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(assignment);
        ScreeningAssignmentDto dto;
        string path;

        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_stageDtos.TryGetValue(assignment.StageId, out var stageDto) || !string.Equals(stageDto.ProjectId, projectId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Stage '{assignment.StageId}' must be saved before assignments can be persisted.");
            }

            dto = ScreeningAssignmentMapper.ToDto(projectId, assignment);
            path = GetAssignmentPath(projectId, assignment.Id);
        }
        finally
        {
            _mutex.Release();
        }

        await WriteJsonAsync(path, dto, ct).ConfigureAwait(false);

        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            _assignmentDtos[assignment.Id] = dto;
            _assignments[assignment.Id] = assignment;
            if (!_assignmentIdsByStage.ContainsKey(assignment.StageId))
            {
                _assignmentIdsByStage[assignment.StageId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            _assignmentIdsByStage[assignment.StageId].Add(assignment.Id);

            RebuildStage(assignment.StageId);
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<ScreeningAssignment?> GetAssignmentAsync(string assignmentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _assignments.TryGetValue(assignmentId, out var assignment) ? assignment : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<IReadOnlyList<ScreeningAssignment>> GetAssignmentsByStageAsync(string stageId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_assignmentIdsByStage.TryGetValue(stageId, out var ids))
            {
                return Array.Empty<ScreeningAssignment>();
            }

            return ids.Select(id => _assignments[id]).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task DeleteAssignmentAsync(string projectId, string assignmentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var path = GetAssignmentPath(projectId, assignmentId);
            DeleteFileIfExists(path);

            if (_assignmentDtos.Remove(assignmentId, out var doc))
            {
                _assignments.Remove(assignmentId);
                if (_assignmentIdsByStage.TryGetValue(doc.StageId, out var ids))
                {
                    ids.Remove(assignmentId);
                    if (ids.Count == 0)
                    {
                        _assignmentIdsByStage.Remove(doc.StageId);
                    }
                }

                RebuildStage(doc.StageId);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task SaveFormResponseAsync(FormResponse response, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_projects.ContainsKey(response.ProjectId))
            {
                throw new InvalidOperationException($"Project '{response.ProjectId}' must be saved before form responses can be persisted.");
            }

            var doc = FormResponseMapper.FromDomain(response, _jsonOptions);
            var path = GetFormResponsePath(response.ProjectId, response.Id);
            await WriteJsonAsync(path, doc, ct).ConfigureAwait(false);

            var key = FormKey(response.ProjectId, response.Id);
            _formDocs[key] = doc;
            _formResponses[key] = response;
            _formIdsByProject.TryAdd(response.ProjectId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _formIdsByProject[response.ProjectId].Add(response.Id);
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<FormResponse?> GetFormResponseAsync(string projectId, string responseId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var key = FormKey(projectId, responseId);
            return _formResponses.TryGetValue(key, out var response) ? response : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task<IReadOnlyList<FormResponse>> GetFormResponsesAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            if (!_formIdsByProject.TryGetValue(projectId, out var ids))
            {
                return Array.Empty<FormResponse>();
            }

            return ids.Select(id => _formResponses[FormKey(projectId, id)]).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    internal async Task DeleteFormResponseAsync(string projectId, string responseId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var path = GetFormResponsePath(projectId, responseId);
            DeleteFileIfExists(path);

            var key = FormKey(projectId, responseId);
            if (_formDocs.Remove(key))
            {
                _formResponses.Remove(key);
            }

            if (_formIdsByProject.TryGetValue(projectId, out var ids))
            {
                ids.Remove(responseId);
                if (ids.Count == 0)
                {
                    _formIdsByProject.Remove(projectId);
                }
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

}
