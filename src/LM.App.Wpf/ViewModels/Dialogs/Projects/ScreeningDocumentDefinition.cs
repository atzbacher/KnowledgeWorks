using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record ScreeningDocumentDefinition
    {
        public ScreeningDocumentDefinition(
            string title,
            string? abstractText,
            IEnumerable<string> authors,
            IEnumerable<DocumentAttributeDefinition> attributes,
            IEnumerable<string> keywords)
        {
            Title = Normalize(title, nameof(title));
            AbstractText = abstractText?.Trim();
            Authors = CreateList(authors, nameof(authors));
            Attributes = CreateAttributes(attributes);
            Keywords = CreateKeywords(keywords);
        }

        public string Title { get; }

        public string? AbstractText { get; }

        public IReadOnlyList<string> Authors { get; }

        public IReadOnlyList<DocumentAttributeDefinition> Attributes { get; }

        public IReadOnlyList<string> Keywords { get; }

        private static string Normalize(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"A non-empty value for '{argumentName}' is required.", argumentName);

            return value.Trim();
        }

        private static IReadOnlyList<string> CreateList(IEnumerable<string> values, string argumentName)
        {
            if (values is null)
                throw new ArgumentNullException(argumentName);

            var list = new List<string>();
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                list.Add(value.Trim());
            }

            return list.AsReadOnly();
        }

        private static IReadOnlyList<DocumentAttributeDefinition> CreateAttributes(IEnumerable<DocumentAttributeDefinition> values)
        {
            if (values is null)
                throw new ArgumentNullException(nameof(values));

            var list = new List<DocumentAttributeDefinition>();
            foreach (var value in values)
            {
                if (value is null)
                    throw new ArgumentException("Attribute collections cannot contain null entries.", nameof(values));

                list.Add(value);
            }

            return list.AsReadOnly();
        }

        private static IReadOnlyList<string> CreateKeywords(IEnumerable<string> values)
        {
            return CreateList(values, nameof(values));
        }
    }
}
