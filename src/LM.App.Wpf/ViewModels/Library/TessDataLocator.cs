using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LM.App.Wpf.ViewModels.Library
{
    internal static class TessDataLocator
    {
        public static string? Resolve(string? workspacePath)
        {
            var candidates = new List<string?>();

            var environmentPrefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
            if (!string.IsNullOrWhiteSpace(environmentPrefix))
            {
                candidates.Add(environmentPrefix);
            }

            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                var root = NormalizePath(workspacePath);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    candidates.Add(Path.Combine(root, ".knowledgeworks", "tessdata"));
                    candidates.Add(Path.Combine(root, ".knowledgeworks"));
                    candidates.Add(Path.Combine(root, "tessdata"));
                    candidates.Add(root);
                }
            }

            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"));

            foreach (var candidate in candidates)
            {
                var resolved = ResolveCandidate(candidate);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            var bootstrapped = TessDataBootstrapper.TryEnsureDefault(workspacePath);
            if (!string.IsNullOrWhiteSpace(bootstrapped))
            {
                return ResolveCandidate(bootstrapped);
            }

            return null;
        }

        private static string? ResolveCandidate(string? candidate)
        {
            var normalized = NormalizePath(candidate);
            if (normalized is null)
            {
                return null;
            }

            if (File.Exists(normalized) && normalized.EndsWith(".traineddata", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(normalized);
                if (!string.IsNullOrWhiteSpace(directory) && ContainsTrainedData(directory))
                {
                    return directory;
                }

                return null;
            }

            if (!Directory.Exists(normalized))
            {
                return null;
            }

            if (ContainsTrainedData(normalized))
            {
                return normalized;
            }

            foreach (var child in EnumerateSubCandidates(normalized))
            {
                var resolved = ResolveCandidate(child);
                if (resolved is not null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateSubCandidates(string directory)
        {
            var direct = Path.Combine(directory, "tessdata");
            if (Directory.Exists(direct))
            {
                yield return direct;
            }

            var knowledgeRoot = Path.Combine(directory, ".knowledgeworks");
            if (Directory.Exists(knowledgeRoot))
            {
                yield return knowledgeRoot;

                var knowledgeTess = Path.Combine(knowledgeRoot, "tessdata");
                if (Directory.Exists(knowledgeTess))
                {
                    yield return knowledgeTess;
                }
            }

            IEnumerable<string> nested = Array.Empty<string>();
            try
            {
                nested = Directory.EnumerateDirectories(directory, "tessdata", SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var child in nested)
            {
                if (Directory.Exists(child))
                {
                    yield return child;
                }
            }
        }

        private static bool ContainsTrainedData(string directory)
        {
            try
            {
                return Directory.EnumerateFiles(directory, "*.traineddata", SearchOption.TopDirectoryOnly).Any();
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
