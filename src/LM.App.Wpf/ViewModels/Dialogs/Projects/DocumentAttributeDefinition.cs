using System;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record DocumentAttributeDefinition
    {
        public DocumentAttributeDefinition(string label, string value)
        {
            Label = Normalize(label, nameof(label));
            Value = Normalize(value, nameof(value));
        }

        public string Label { get; }

        public string Value { get; }

        private static string Normalize(string input, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return input.Trim();
        }
    }
}
