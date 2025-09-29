using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Models;

namespace LM.Core.Abstractions;

public interface ILibraryAnnotationRepository
{
    Task<IReadOnlyList<LibraryAnnotation>> GetAnnotationsAsync(string entryId,
                                                               string attachmentId,
                                                               CancellationToken cancellationToken = default);

    Task<LibraryAnnotation?> GetAnnotationAsync(string entryId,
                                                string attachmentId,
                                                Guid annotationId,
                                                CancellationToken cancellationToken = default);

    Task UpsertAsync(LibraryAnnotation annotation, CancellationToken cancellationToken = default);

    Task ReplaceAsync(string entryId,
                      string attachmentId,
                      IEnumerable<LibraryAnnotation> annotations,
                      CancellationToken cancellationToken = default);

    Task DeleteAsync(string entryId,
                     string attachmentId,
                     Guid annotationId,
                     CancellationToken cancellationToken = default);
}
