using System;
using System.Collections.Generic;
using LM.Core.Models;

namespace LM.App.Wpf.Library.Search
{
    internal enum LibrarySearchField
    {
        Any,
        Title,
        Author,
        Tags,
        Source,
        InternalId,
        Doi,
        Pmid,
        Nct,
        Type,
        Year,
        AddedBy,
        AddedOn,
        Internal,
        Notes
    }

    internal static class LibrarySearchFieldMap
    {
        private static readonly Dictionary<string, LibrarySearchField> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = LibrarySearchField.Title,
            ["t"] = LibrarySearchField.Title,
            ["author"] = LibrarySearchField.Author,
            ["authors"] = LibrarySearchField.Author,
            ["a"] = LibrarySearchField.Author,
            ["tag"] = LibrarySearchField.Tags,
            ["tags"] = LibrarySearchField.Tags,
            ["source"] = LibrarySearchField.Source,
            ["journal"] = LibrarySearchField.Source,
            ["internalid"] = LibrarySearchField.InternalId,
            ["id"] = LibrarySearchField.InternalId,
            ["doi"] = LibrarySearchField.Doi,
            ["pmid"] = LibrarySearchField.Pmid,
            ["nct"] = LibrarySearchField.Nct,
            ["type"] = LibrarySearchField.Type,
            ["entrytype"] = LibrarySearchField.Type,
            ["year"] = LibrarySearchField.Year,
            ["addedby"] = LibrarySearchField.AddedBy,
            ["createdby"] = LibrarySearchField.AddedBy,
            ["addedon"] = LibrarySearchField.AddedOn,
            ["created"] = LibrarySearchField.AddedOn,
            ["internal"] = LibrarySearchField.Internal,
            ["visibility"] = LibrarySearchField.Internal,
            ["notes"] = LibrarySearchField.Notes,
            ["summary"] = LibrarySearchField.Notes
        };

        public static LibrarySearchField Resolve(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return LibrarySearchField.Any;
            }

            if (_map.TryGetValue(token.Trim(), out var field))
            {
                return field;
            }

            if (Enum.TryParse<EntryType>(token, ignoreCase: true, out _))
            {
                return LibrarySearchField.Type;
            }

            return LibrarySearchField.Any;
        }
    }
}
