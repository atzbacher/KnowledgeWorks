using System;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record ScreeningCriterionDefinition
    {
        public ScreeningCriterionDefinition(string key, string label, string? description, bool isDefaultSelected)
        {
            Key = NormalizeKey(key);
            Label = Normalize(label, nameof(label));
            Description = description?.Trim();
            IsDefaultSelected = isDefaultSelected;
        }

        public string Key { get; }

        public string Label { get; }

        public string? Description { get; }

        public bool IsDefaultSelected { get; }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty key is required.", nameof(value));

            return value.Trim();
        }

        private static string Normalize(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return value.Trim();
        }
    }
}
