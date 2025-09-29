#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;
using LM.Core.Models;

namespace LM.Infrastructure.Repositories;

public sealed class JsonLibraryAnnotationRepository : ILibraryAnnotationRepository
{
    private readonly IWorkSpaceService _workspace;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonLibraryAnnotationRepository(IWorkSpaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<IReadOnlyList<LibraryAnnotation>> GetAnnotationsAsync(string entryId,
                                                                           string attachmentId,
                                                                           CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(entryId);
        var safeAttachmentId = NormalizeAttachmentId(attachmentId);
        var path = GetAnnotationsPath(entryId, safeAttachmentId, ensureDirectory: false);

        if (!File.Exists(path))
        {
            return Array.Empty<LibraryAnnotation>();
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = await JsonSerializer.DeserializeAsync<AnnotationDocument>(stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (document?.Annotations is null || document.Annotations.Count == 0)
        {
            return Array.Empty<LibraryAnnotation>();
        }

        return document.Annotations;
    }

    public async Task<LibraryAnnotation?> GetAnnotationAsync(string entryId,
                                                             string attachmentId,
                                                             Guid annotationId,
                                                             CancellationToken cancellationToken = default)
    {
        if (annotationId == Guid.Empty)
        {
            return null;
        }

        var annotations = await GetAnnotationsAsync(entryId, attachmentId, cancellationToken).ConfigureAwait(false);
        return annotations.FirstOrDefault(annotation => annotation.AnnotationId == annotationId);
    }

    public async Task UpsertAsync(LibraryAnnotation annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        var annotations = await LoadMutableAsync(annotation.EntryId, annotation.AttachmentId, cancellationToken)
            .ConfigureAwait(false);

        var index = annotations.FindIndex(existing => existing.AnnotationId == annotation.AnnotationId);
        if (index >= 0)
        {
            annotations[index] = annotation;
        }
        else
        {
            annotations.Add(annotation);
        }

        await PersistAsync(annotation.EntryId, annotation.AttachmentId, annotations, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceAsync(string entryId,
                                   string attachmentId,
                                   IEnumerable<LibraryAnnotation> annotations,
                                   CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ValidateIdentifiers(entryId);
        var safeAttachmentId = NormalizeAttachmentId(attachmentId);

        var materialized = annotations
            .Where(static annotation => annotation is not null)
            .Select(static annotation => annotation!)
            .ToList();

        await PersistAsync(entryId, safeAttachmentId, materialized, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string entryId,
                                  string attachmentId,
                                  Guid annotationId,
                                  CancellationToken cancellationToken = default)
    {
        if (annotationId == Guid.Empty)
        {
            return;
        }

        var annotations = await LoadMutableAsync(entryId, attachmentId, cancellationToken).ConfigureAwait(false);
        var removed = annotations.RemoveAll(annotation => annotation.AnnotationId == annotationId);
        if (removed == 0)
        {
            return;
        }

        await PersistAsync(entryId, attachmentId, annotations, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<LibraryAnnotation>> LoadMutableAsync(string entryId,
                                                                string attachmentId,
                                                                CancellationToken cancellationToken)
    {
        ValidateIdentifiers(entryId);
        var safeAttachmentId = NormalizeAttachmentId(attachmentId);
        var path = GetAnnotationsPath(entryId, safeAttachmentId, ensureDirectory: false);

        if (!File.Exists(path))
        {
            return new List<LibraryAnnotation>();
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = await JsonSerializer.DeserializeAsync<AnnotationDocument>(stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return document?.Annotations?.ToList() ?? new List<LibraryAnnotation>();
    }

    private async Task PersistAsync(string entryId,
                                    string attachmentId,
                                    List<LibraryAnnotation> annotations,
                                    CancellationToken cancellationToken)
    {
        ValidateIdentifiers(entryId);
        var safeAttachmentId = NormalizeAttachmentId(attachmentId);
        var path = GetAnnotationsPath(entryId, safeAttachmentId, ensureDirectory: true);
        var lockPath = path + ".lock";
        var tmpPath = path + ".tmp";

        FileStream? lockStream = null;
        try
        {
            lockStream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using (lockStream.ConfigureAwait(false))
            {
                // Intentionally left blank. Ownership of the handle enforces exclusivity.
            }

            await using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var document = new AnnotationDocument { Annotations = annotations };
                await JsonSerializer.SerializeAsync(stream, document, _serializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tmpPath, path);
        }
        finally
        {
            try
            {
                File.Delete(lockPath);
            }
            catch
            {
                // Ignore cleanup failures.
            }

            try
            {
                if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private string GetAnnotationsPath(string entryId, string attachmentId, bool ensureDirectory)
    {
        var root = _workspace.GetWorkspaceRoot();
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Workspace root has not been configured.");
        }

        var entryDirectory = Path.Combine(root, "entries", entryId);
        var annotationsDirectory = Path.Combine(entryDirectory, "annotations");
        if (ensureDirectory)
        {
            Directory.CreateDirectory(annotationsDirectory);
        }

        return Path.Combine(annotationsDirectory, attachmentId + ".json");
    }

    private static string NormalizeAttachmentId(string attachmentId)
    {
        return string.IsNullOrWhiteSpace(attachmentId) ? "__entry__" : attachmentId.Trim();
    }

    private static void ValidateIdentifiers(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("Entry identifier is required.", nameof(entryId));
        }
    }

    private sealed class AnnotationDocument
    {
        public List<LibraryAnnotation> Annotations { get; set; } = new();
    }
}
