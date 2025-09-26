using System;

namespace LM.Review.Core.Validation;

public sealed class FormSchemaIssue
{
    private FormSchemaIssue(
        string code,
        string message,
        FormSchemaSeverity severity,
        string? sectionId,
        string? fieldId)
    {
        Code = code;
        Message = message;
        Severity = severity;
        SectionId = sectionId;
        FieldId = fieldId;
    }

    public string Code { get; }

    public string Message { get; }

    public FormSchemaSeverity Severity { get; }

    public string? SectionId { get; }

    public string? FieldId { get; }

    public static FormSchemaIssue Error(string code, string message, string? sectionId = null, string? fieldId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new FormSchemaIssue(code.Trim(), message.Trim(), FormSchemaSeverity.Error, sectionId, fieldId);
    }

    public static FormSchemaIssue Warning(string code, string message, string? sectionId = null, string? fieldId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new FormSchemaIssue(code.Trim(), message.Trim(), FormSchemaSeverity.Warning, sectionId, fieldId);
    }
}
