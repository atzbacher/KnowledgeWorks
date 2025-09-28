using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Dialogs.Projects
{
    public sealed record DataExtractionGroupDefinition
    {
        public DataExtractionGroupDefinition(string name, IEnumerable<DataExtractionFieldDefinition> fields)
        {
            Name = NormalizeName(name);
            Fields = CreateFields(fields);
        }

        public string Name { get; }

        public IReadOnlyList<DataExtractionFieldDefinition> Fields { get; }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A non-empty group name is required.", nameof(name));

            return name.Trim();
        }

        private static IReadOnlyList<DataExtractionFieldDefinition> CreateFields(IEnumerable<DataExtractionFieldDefinition> fields)
        {
            if (fields is null)
                throw new ArgumentNullException(nameof(fields));

            var list = new List<DataExtractionFieldDefinition>();
            foreach (var field in fields)
            {
                if (field is null)
                    throw new ArgumentException("Field definitions cannot contain null entries.", nameof(fields));

                list.Add(field);
            }

            return list.AsReadOnly();
        }
    }
}
