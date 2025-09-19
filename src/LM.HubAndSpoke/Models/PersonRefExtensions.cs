#nullable enable
using System;

namespace LM.HubSpoke.Models
{
    internal static class PersonRefExtensions
    {
        public static string? ToDisplayString(this PersonRef person)
        {
            var candidate = !string.IsNullOrWhiteSpace(person.DisplayName)
                ? person.DisplayName
                : person.Id;

            if (string.IsNullOrWhiteSpace(candidate))
                return null;

            return string.Equals(candidate, "unknown", StringComparison.OrdinalIgnoreCase)
                ? null
                : candidate.Trim();
        }
    }
}
