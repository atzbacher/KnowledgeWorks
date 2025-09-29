using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LM.Core.Abstractions;

namespace LM.Infrastructure.Pdf
{
    public sealed class PdfAnnotationPreviewStorage : IPdfAnnotationPreviewStorage
    {
        private const string PreviewExtension = ".png";

        private readonly IWorkSpaceService _workspace;

        public PdfAnnotationPreviewStorage(IWorkSpaceService workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public async Task<string> SaveAsync(string pdfHash, string annotationId, byte[] pngBytes, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pdfHash))
            {
                throw new ArgumentException("PDF hash must be provided.", nameof(pdfHash));
            }

            if (pdfHash.Length < 2)
            {
                throw new ArgumentException("PDF hash must contain at least two characters.", nameof(pdfHash));
            }

            if (string.IsNullOrWhiteSpace(annotationId))
            {
                throw new ArgumentException("Annotation identifier must be provided.", nameof(annotationId));
            }

            if (pngBytes is null || pngBytes.Length == 0)
            {
                throw new ArgumentException("PNG payload must be provided.", nameof(pngBytes));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var sanitizedAnnotationId = SanitizeAnnotationId(annotationId);
            if (sanitizedAnnotationId is null)
            {
                throw new ArgumentException("Annotation identifier contained only invalid characters.", nameof(annotationId));
            }

            var relativeRoot = Path.Combine("extraction", pdfHash);
            var relativePath = NormalizeRelativePath(Path.Combine(relativeRoot, sanitizedAnnotationId + PreviewExtension));
            var absolutePath = _workspace.GetAbsolutePath(relativePath);

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(absolutePath, pngBytes, cancellationToken).ConfigureAwait(false);

            return relativePath;
        }

        private static string? SanitizeAnnotationId(string annotationId)
        {
            var trimmed = annotationId.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            var normalizedSeparators = trimmed
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            var fileName = Path.GetFileName(normalizedSeparators);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            Span<char> buffer = stackalloc char[fileName.Length];
            var length = 0;

            foreach (var ch in fileName)
            {
                if (Array.IndexOf(invalidChars, ch) >= 0 || char.IsWhiteSpace(ch))
                {
                    continue;
                }

                buffer[length++] = ch;
            }

            if (length == 0)
            {
                return null;
            }

            return new string(buffer[..length]);
        }

        private static string NormalizeRelativePath(string path)
        {
            var interim = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return interim.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
