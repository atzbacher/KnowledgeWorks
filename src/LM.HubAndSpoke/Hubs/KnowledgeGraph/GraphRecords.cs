#nullable enable
using System.Collections.Generic;

namespace LM.HubSpoke.Hubs.KnowledgeGraph
{
    public sealed class MortalityComparison
    {
        public MortalityComparison(
            string entryId,
            string entryTitle,
            string endpointId,
            string endpointName,
            string? populationId,
            string? interventionId,
            string? interventionName,
            string? comparatorInterventionId,
            string? comparatorName,
            double? value,
            string? unit,
            string? metric,
            string? timepoint)
        {
            EntryId = entryId;
            EntryTitle = entryTitle;
            EndpointId = endpointId;
            EndpointName = endpointName;
            PopulationId = populationId;
            InterventionId = interventionId;
            InterventionName = interventionName;
            ComparatorInterventionId = comparatorInterventionId;
            ComparatorName = comparatorName;
            Value = value;
            Unit = unit;
            Metric = metric;
            Timepoint = timepoint;
        }

        public string EntryId { get; }
        public string EntryTitle { get; }
        public string EndpointId { get; }
        public string EndpointName { get; }
        public string? PopulationId { get; }
        public string? InterventionId { get; }
        public string? InterventionName { get; }
        public string? ComparatorInterventionId { get; }
        public string? ComparatorName { get; }
        public double? Value { get; }
        public string? Unit { get; }
        public string? Metric { get; }
        public string? Timepoint { get; }
    }

    public sealed class KaplanMeierOverlay
    {
        public KaplanMeierOverlay(
            string entryId,
            string entryTitle,
            string endpointId,
            string endpointName,
            string? populationId,
            string? interventionId,
            string? interventionName,
            IReadOnlyList<KaplanMeierPointDto> curve)
        {
            EntryId = entryId;
            EntryTitle = entryTitle;
            EndpointId = endpointId;
            EndpointName = endpointName;
            PopulationId = populationId;
            InterventionId = interventionId;
            InterventionName = interventionName;
            Curve = curve;
        }

        public string EntryId { get; }
        public string EntryTitle { get; }
        public string EndpointId { get; }
        public string EndpointName { get; }
        public string? PopulationId { get; }
        public string? InterventionId { get; }
        public string? InterventionName { get; }
        public IReadOnlyList<KaplanMeierPointDto> Curve { get; }
    }

    public sealed class KaplanMeierPointDto
    {
        public KaplanMeierPointDto(double time, double survivalProbability)
        {
            Time = time;
            SurvivalProbability = survivalProbability;
        }

        public double Time { get; }
        public double SurvivalProbability { get; }
    }

    public sealed class BaselineCharacteristicHit
    {
        public BaselineCharacteristicHit(
            string entryId,
            string entryTitle,
            string populationId,
            string populationName,
            string characteristic,
            string value)
        {
            EntryId = entryId;
            EntryTitle = entryTitle;
            PopulationId = populationId;
            PopulationName = populationName;
            Characteristic = characteristic;
            Value = value;
        }

        public string EntryId { get; }
        public string EntryTitle { get; }
        public string PopulationId { get; }
        public string PopulationName { get; }
        public string Characteristic { get; }
        public string Value { get; }
    }

    public sealed class GraphPopulationNode
    {
        public GraphPopulationNode(string populationId, string name, string? description)
        {
            PopulationId = populationId;
            Name = name;
            Description = description;
        }

        public string PopulationId { get; }
        public string Name { get; }
        public string? Description { get; }
    }

    public sealed class GraphInterventionNode
    {
        public GraphInterventionNode(string interventionId, string name, string? type, string? description)
        {
            InterventionId = interventionId;
            Name = name;
            Type = type;
            Description = description;
        }

        public string InterventionId { get; }
        public string Name { get; }
        public string? Type { get; }
        public string? Description { get; }
    }

    public sealed class GraphEndpointNode
    {
        public GraphEndpointNode(string endpointId, string name, string category, string? description)
        {
            EndpointId = endpointId;
            Name = name;
            Category = category;
            Description = description;
        }

        public string EndpointId { get; }
        public string Name { get; }
        public string Category { get; }
        public string? Description { get; }
    }

    public sealed class GraphEdge
    {
        public GraphEdge(string sourceType, string sourceId, string targetType, string targetId, string relationship, string? payloadJson)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            TargetType = targetType;
            TargetId = targetId;
            Relationship = relationship;
            PayloadJson = payloadJson;
        }

        public string SourceType { get; }
        public string SourceId { get; }
        public string TargetType { get; }
        public string TargetId { get; }
        public string Relationship { get; }
        public string? PayloadJson { get; }
    }

    public sealed class GraphEntryOverview
    {
        public GraphEntryOverview(
            string entryId,
            string title,
            IReadOnlyList<GraphPopulationNode> populations,
            IReadOnlyList<GraphInterventionNode> interventions,
            IReadOnlyList<GraphEndpointNode> endpoints,
            IReadOnlyList<GraphEdge> edges)
        {
            EntryId = entryId;
            Title = title;
            Populations = populations;
            Interventions = interventions;
            Endpoints = endpoints;
            Edges = edges;
        }

        public string EntryId { get; }
        public string Title { get; }
        public IReadOnlyList<GraphPopulationNode> Populations { get; }
        public IReadOnlyList<GraphInterventionNode> Interventions { get; }
        public IReadOnlyList<GraphEndpointNode> Endpoints { get; }
        public IReadOnlyList<GraphEdge> Edges { get; }
    }
}
