using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record ScreeningStageDefinition
    {
        public ScreeningStageDefinition(
            string name,
            string description,
            string includeLabel,
            string excludeLabel,
            bool allowMultipleCriteria,
            bool allowNotes,
            IEnumerable<ScreeningCriterionDefinition> criteria)
        {
            Name = Normalize(name, nameof(name));
            Description = description?.Trim() ?? string.Empty;
            IncludeLabel = Normalize(includeLabel, nameof(includeLabel));
            ExcludeLabel = Normalize(excludeLabel, nameof(excludeLabel));
            AllowMultipleCriteria = allowMultipleCriteria;
            AllowNotes = allowNotes;
            Criteria = CreateCriteria(criteria);
        }

        public string Name { get; }

        public string Description { get; }

        public string IncludeLabel { get; }

        public string ExcludeLabel { get; }

        public bool AllowMultipleCriteria { get; }

        public bool AllowNotes { get; }

        public IReadOnlyList<ScreeningCriterionDefinition> Criteria { get; }

        private static string Normalize(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return value.Trim();
        }

        private static IReadOnlyList<ScreeningCriterionDefinition> CreateCriteria(IEnumerable<ScreeningCriterionDefinition> criteria)
        {
            if (criteria is null)
                throw new ArgumentNullException(nameof(criteria));

            var list = new List<ScreeningCriterionDefinition>();
            foreach (var criterion in criteria)
            {
                if (criterion is null)
                    throw new ArgumentException("Criteria collections cannot contain null entries.", nameof(criteria));

                list.Add(criterion);
            }

            return list.AsReadOnly();
        }
    }
}
