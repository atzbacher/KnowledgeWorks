namespace LM.Review.Core.Models;

using System;
using System.Collections.Generic;

public enum ReviewProjectType
{
    Picos,
    Custom
}

public enum ReviewLayerKind
{
    TitleAbstractScreening,
    FullTextScreening,
    DataExtraction,
    Custom
}

public enum ReviewLayerDisplayMode
{
    Picos,
    Custom
}

public sealed record ReviewLayerDefinition(
    string Name,
    ReviewLayerKind Kind,
    ReviewLayerDisplayMode DisplayMode,
    IReadOnlyList<string> DisplayFields,
    string? Instructions);

public sealed record ReviewProjectDefinition(
    string Title,
    string CreatedBy,
    string? LitSearchRunId,
    ReviewProjectType ProjectType,
    IReadOnlyList<ReviewLayerDefinition> Layers,
    string? MetadataNotes,
    DateTimeOffset CreatedAt);
