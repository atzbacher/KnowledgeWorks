#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using LM.Core.Abstractions;

namespace LM.App.Wpf.Services.Review;

internal interface IReviewCreationDiagnostics
{
    void RecordStep(string message);

    void RecordException(string message, Exception exception);
}

internal sealed class ReviewCreationDiagnostics : IReviewCreationDiagnostics
{
    private readonly IWorkSpaceService _workspace;
    private readonly Lazy<string> _logPath;
    private readonly object _gate = new();

    public ReviewCreationDiagnostics(IWorkSpaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logPath = new Lazy<string>(ResolveLogPath, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void RecordStep(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteLine("INFO", message.Trim());
    }

    public void RecordException(string message, Exception exception)
    {
        if (exception is null)
        {
            return;
        }

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append(message!.Trim());
            builder.Append(':');
            builder.Append(' ');
        }

        builder.Append(exception.GetType().Name);
        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            builder.Append(" – ");
            builder.Append(exception.Message.Trim());
        }

        WriteLine("ERROR", builder.ToString(), exception);
    }

    private string ResolveLogPath()
    {
        try
        {
            var root = _workspace.GetWorkspaceRoot();
            var logDirectory = Path.Combine(root, "logs");
            Directory.CreateDirectory(logDirectory);
            return Path.Combine(logDirectory, "review-creation.log");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PathTooLongException)
        {
            // Fall back to the temp directory if the workspace is not accessible.
            var fallbackDirectory = Path.Combine(Path.GetTempPath(), "kw-review-diagnostics");
            Directory.CreateDirectory(fallbackDirectory);
            return Path.Combine(fallbackDirectory, "review-creation.log");
        }
    }

    private void WriteLine(string level, string message, Exception? exception = null)
    {
        try
        {
            var line = BuildLine(level, message, exception);
            lock (_gate)
            {
                File.AppendAllText(_logPath.Value, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never throw back into the application flow.
        }
    }

    private static string BuildLine(string level, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append('[');
        builder.Append(level);
        builder.Append(']');
        builder.Append(' ');
        builder.Append(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception.ToString());
        }
        else
        {
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
