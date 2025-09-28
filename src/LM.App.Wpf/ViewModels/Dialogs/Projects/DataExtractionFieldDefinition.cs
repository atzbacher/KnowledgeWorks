using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record DataExtractionFieldDefinition
    {
        public DataExtractionFieldDefinition(
            string key,
            string label,
            string templateKey,
            string? placeholder,
            IEnumerable<string>? options,
            string? defaultValue,
            bool isRequired)
        {
            Key = NormalizeKey(key);
            Label = NormalizeRequired(label, nameof(label));
            TemplateKey = NormalizeRequired(templateKey, nameof(templateKey));
            Placeholder = placeholder?.Trim();
            DefaultValue = defaultValue?.Trim();
            Options = CreateOptions(options);
            IsRequired = isRequired;
        }

        public string Key { get; }

        public string Label { get; }

        public string TemplateKey { get; }

        public string? Placeholder { get; }

        public IReadOnlyList<string> Options { get; }

        public string? DefaultValue { get; }

        public bool IsRequired { get; }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty key is required.", nameof(value));

            return value.Trim();
        }

        private static string NormalizeRequired(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return value.Trim();
        }

        private static IReadOnlyList<string> CreateOptions(IEnumerable<string>? options)
        {
            if (options is null)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();
            foreach (var option in options)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    continue;
                }

                list.Add(option.Trim());
            }

            return list.AsReadOnly();
        }
    }
}
