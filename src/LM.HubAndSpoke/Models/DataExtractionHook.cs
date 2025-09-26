#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LM.HubSpoke.Models
{
    /// <summary>
    /// Structured data extracted from a study that is shared across modules via the extraction hub.
    /// </summary>
    public sealed class DataExtractionHook
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; } = "1.0.0";

        [JsonPropertyName("entry_id")]
        public string EntryId { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("year")]
        public int? Year { get; init; }

        [JsonPropertyName("source")]
        public string? Source { get; init; }

        [JsonPropertyName("populations")]
        public IReadOnlyList<ExtractedPopulation> Populations { get; init; } = Array.Empty<ExtractedPopulation>();

        [JsonPropertyName("interventions")]
        public IReadOnlyList<ExtractedIntervention> Interventions { get; init; } = Array.Empty<ExtractedIntervention>();

        [JsonPropertyName("assignments")]
        public IReadOnlyList<PopulationInterventionAssignment> Assignments { get; init; } = Array.Empty<PopulationInterventionAssignment>();

        [JsonPropertyName("endpoints")]
        public IReadOnlyList<ExtractedEndpoint> Endpoints { get; init; } = Array.Empty<ExtractedEndpoint>();
    }

    public sealed class ExtractedPopulation
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("baseline_characteristics")]
        public IReadOnlyDictionary<string, string> BaselineCharacteristics { get; init; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ExtractedIntervention
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("attributes")]
        public IReadOnlyDictionary<string, string>? Attributes { get; init; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PopulationInterventionAssignment
    {
        [JsonPropertyName("population_id")]
        public string PopulationId { get; init; } = string.Empty;

        [JsonPropertyName("intervention_id")]
        public string InterventionId { get; init; } = string.Empty;

        [JsonPropertyName("arm_label")]
        public string? ArmLabel { get; init; }
    }

    public sealed class ExtractedEndpoint
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("readouts")]
        public IReadOnlyList<EndpointReadout> Readouts { get; init; } = Array.Empty<EndpointReadout>();
    }

    public sealed class EndpointReadout
    {
        [JsonPropertyName("population_id")]
        public string? PopulationId { get; init; }

        [JsonPropertyName("intervention_id")]
        public string? InterventionId { get; init; }

        [JsonPropertyName("comparator_intervention_id")]
        public string? ComparatorInterventionId { get; init; }

        [JsonPropertyName("metric")]
        public string? Metric { get; init; }

        [JsonPropertyName("value")]
        public double? Value { get; init; }

        [JsonPropertyName("unit")]
        public string? Unit { get; init; }

        [JsonPropertyName("timepoint")]
        public string? Timepoint { get; init; }

        [JsonPropertyName("curve")]
        public IReadOnlyList<KaplanMeierPoint>? KaplanMeierCurve { get; init; }
    }

    public sealed class KaplanMeierPoint
    {
        [JsonPropertyName("time")]
        public double Time { get; init; }

        [JsonPropertyName("survival_probability")]
        public double SurvivalProbability { get; init; }
    }
}
