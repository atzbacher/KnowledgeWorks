#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using LM.App.Wpf.ViewModels;
using HookM = LM.HubSpoke.Models;

namespace LM.App.Wpf.ViewModels.Dialogs.Staging
{
    internal interface IDataExtractionCommitBuilder
    {
        HookM.DataExtractionHook? Build(StagingItem item,
                                        IEnumerable<StagingTableRowViewModel>? tables,
                                        IEnumerable<StagingEndpointViewModel>? endpoints);
    }

    internal sealed class DataExtractionCommitBuilder : IDataExtractionCommitBuilder
    {
        public HookM.DataExtractionHook? Build(StagingItem item,
                                               IEnumerable<StagingTableRowViewModel>? tables,
                                               IEnumerable<StagingEndpointViewModel>? endpoints)
        {
            if (item is null)
                throw new ArgumentNullException(nameof(item));

            var source = item.DataExtractionHook;
            if (source is null)
                return null;

            var tableSnapshots = tables?.Select(static t => t?.Snapshot)
                                       .Where(static t => t is not null)
                                       .Cast<HookM.DataExtractionTable>()
                                       .Select(CloneTable)
                                       .ToList()
                                ?? new List<HookM.DataExtractionTable>();

            if (tableSnapshots.Count == 0 && source.Tables is { Count: > 0 })
                tableSnapshots = source.Tables.Select(CloneTable).ToList();

            var endpointSnapshots = endpoints?.Select(static e => e?.Endpoint)
                                              .Where(static e => e is not null)
                                              .Cast<HookM.DataExtractionEndpoint>()
                                              .Select(CloneEndpoint)
                                              .ToList()
                                   ?? new List<HookM.DataExtractionEndpoint>();

            if (endpointSnapshots.Count == 0 && source.Endpoints is { Count: > 0 })
                endpointSnapshots = source.Endpoints.Select(CloneEndpoint).ToList();

            return new HookM.DataExtractionHook
            {
                SchemaVersion = source.SchemaVersion,
                ExtractedAtUtc = DateTime.UtcNow,
                ExtractedBy = GetCurrentUserName(),
                Populations = source.Populations.Select(ClonePopulation).ToList(),
                Interventions = source.Interventions.Select(CloneIntervention).ToList(),
                Endpoints = endpointSnapshots,
                Figures = source.Figures.Select(CloneFigure).ToList(),
                Tables = tableSnapshots,
                Notes = source.Notes
            };
        }

        private static HookM.DataExtractionPopulation ClonePopulation(HookM.DataExtractionPopulation population)
            => new()
            {
                Id = population.Id,
                Label = population.Label,
                SampleSize = population.SampleSize,
                InclusionCriteria = population.InclusionCriteria,
                ExclusionCriteria = population.ExclusionCriteria,
                Notes = population.Notes
            };

        private static HookM.DataExtractionIntervention CloneIntervention(HookM.DataExtractionIntervention intervention)
            => new()
            {
                Id = intervention.Id,
                Name = intervention.Name,
                PopulationIds = new List<string>(intervention.PopulationIds),
                Dosage = intervention.Dosage,
                Comparator = intervention.Comparator,
                Notes = intervention.Notes
            };

        private static HookM.DataExtractionEndpoint CloneEndpoint(HookM.DataExtractionEndpoint endpoint)
            => new()
            {
                Id = endpoint.Id,
                Name = endpoint.Name,
                Description = endpoint.Description,
                Timepoint = endpoint.Timepoint,
                Measure = endpoint.Measure,
                PopulationIds = new List<string>(endpoint.PopulationIds),
                InterventionIds = new List<string>(endpoint.InterventionIds),
                ResultSummary = endpoint.ResultSummary,
                EffectSize = endpoint.EffectSize,
                Notes = endpoint.Notes,
                Confirmed = endpoint.Confirmed
            };

        private static HookM.DataExtractionTable CloneTable(HookM.DataExtractionTable table)
            => new()
            {
                Id = table.Id,
                Title = table.Title,
                Caption = table.Caption,
                SourcePath = table.SourcePath,
                Pages = new List<string>(table.Pages),
                LinkedEndpointIds = new List<string>(table.LinkedEndpointIds),
                LinkedInterventionIds = new List<string>(table.LinkedInterventionIds),
                ProvenanceHash = table.ProvenanceHash,
                Notes = table.Notes,
                TableLabel = table.TableLabel,
                Summary = table.Summary
            };

        private static HookM.DataExtractionFigure CloneFigure(HookM.DataExtractionFigure figure)
            => new()
            {
                Id = figure.Id,
                Title = figure.Title,
                Caption = figure.Caption,
                SourcePath = figure.SourcePath,
                Pages = new List<string>(figure.Pages),
                ProvenanceHash = figure.ProvenanceHash,
                Notes = figure.Notes,
                ThumbnailPath = figure.ThumbnailPath
            };

        private static string GetCurrentUserName()
        {
            var user = Environment.UserName;
            return string.IsNullOrWhiteSpace(user) ? "unknown" : user;
        }
    }
}
