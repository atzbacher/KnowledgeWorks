using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LM.App.Wpf.ViewModels.Library
{
    internal static class TessDataBootstrapper
    {
        private const string DefaultLanguage = "eng";
        private const string DefaultFileName = DefaultLanguage + ".traineddata";
        private static readonly Uri DefaultModelUri = new("https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata", UriKind.Absolute);
        private const string SourceOverrideVariable = "KNOWLEDGEWORKS_TESSDATA_URL";
        private const string DirectoryOverrideVariable = "KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DIR";
        private const string DisableVariable = "KNOWLEDGEWORKS_TESSDATA_BOOTSTRAP_DISABLED";

        public static string? TryEnsureDefault(string? workspacePath)
        {
            if (IsDisabled())
            {
                return null;
            }

            var targetDirectory = ResolveTargetDirectory(workspacePath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(targetDirectory);
                var targetFile = Path.Combine(targetDirectory, DefaultFileName);
                if (IsValidTrainingData(targetFile))
                {
                    return targetDirectory;
                }

                var source = ResolveSourceUri();
                if (source is null)
                {
                    return null;
                }

                var tempFile = Path.Combine(targetDirectory, $"{DefaultFileName}.{Guid.NewGuid():N}.tmp");
                try
                {
                    DownloadTrainingData(source, tempFile);
                    if (!IsValidTrainingData(tempFile))
                    {
                        return null;
                    }

                    File.Move(tempFile, targetFile, overwrite: true);
                    return targetDirectory;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        private static bool IsDisabled()
        {
            var value = Environment.GetEnvironmentVariable(DisableVariable);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveTargetDirectory(string? workspacePath)
        {
            var overridePath = Environment.GetEnvironmentVariable(DirectoryOverrideVariable);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                var resolved = NormalizePath(overridePath);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            var workspaceRoot = NormalizePath(workspacePath);
            if (!string.IsNullOrWhiteSpace(workspaceRoot))
            {
                return Path.Combine(workspaceRoot, ".knowledgeworks", "tessdata");
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                return null;
            }

            return Path.Combine(localAppData, "KnowledgeWorks", "tessdata");
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

        private static Uri? ResolveSourceUri()
        {
            var overrideValue = Environment.GetEnvironmentVariable(SourceOverrideVariable);
            if (!string.IsNullOrWhiteSpace(overrideValue))
            {
                if (Uri.TryCreate(overrideValue, UriKind.Absolute, out var explicitUri))
                {
                    return explicitUri;
                }

                var normalized = NormalizePath(overrideValue);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return new Uri(normalized);
                }
            }

            return DefaultModelUri;
        }

        private static void DownloadTrainingData(Uri source, string destination)
        {
            if (source.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = source.LocalPath;
                if (!File.Exists(localPath))
                {
                    throw new FileNotFoundException($"Training data source was not found at '{localPath}'.");
                }

                File.Copy(localPath, destination, overwrite: true);
                return;
            }

            using var client = CreateHttpClient();
            using var stream = client.GetStreamAsync(source).GetAwaiter().GetResult();
            using var file = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.CopyTo(file);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KnowledgeWorks/1.0");
            return client;
        }

        private static bool IsValidTrainingData(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists && info.Length > 1024;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
