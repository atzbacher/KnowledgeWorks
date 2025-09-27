namespace LM.App.Wpf.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;

internal sealed class ReviewAuditService : IReviewAuditService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _logFile;

    public ReviewAuditService()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.CurrentDirectory;
        }

        var directory = Path.Combine(root, "KnowledgeWorks", "review-audit");
        Directory.CreateDirectory(directory);
        _logFile = Path.Combine(directory, "review-changelog.jsonl");
    }

    public void Append(ReviewProjectDefinition project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var json = JsonSerializer.Serialize(project, s_jsonOptions);
        File.AppendAllText(_logFile, json + Environment.NewLine);
    }
}
