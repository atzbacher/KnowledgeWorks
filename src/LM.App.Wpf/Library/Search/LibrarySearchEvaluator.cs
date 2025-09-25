using System;
using System.Collections.Generic;
using System.Linq;
using LM.Core.Models;

namespace LM.App.Wpf.Library.Search
{
    internal sealed class LibrarySearchEvaluator
    {
        private static readonly StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

        public bool Matches(Entry entry, LibrarySearchNode? node)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (node is null)
            {
                return true;
            }

            return EvaluateNode(entry, node);
        }

        private bool EvaluateNode(Entry entry, LibrarySearchNode node)
        {
            switch (node)
            {
                case LibrarySearchTermNode termNode:
                    return EvaluateTerm(entry, termNode.Term);
                case LibrarySearchUnaryNode unaryNode:
                    return unaryNode.Operator == LibrarySearchUnaryOperator.Not
                        ? !EvaluateNode(entry, unaryNode.Operand)
                        : EvaluateNode(entry, unaryNode.Operand);
                case LibrarySearchBinaryNode binaryNode:
                    var left = EvaluateNode(entry, binaryNode.Left);
                    var right = EvaluateNode(entry, binaryNode.Right);
                    return binaryNode.Operator == LibrarySearchBinaryOperator.And
                        ? left && right
                        : left || right;
                default:
                    return true;
            }
        }

        private bool EvaluateTerm(Entry entry, LibrarySearchTerm term)
        {
            return term.Field switch
            {
                LibrarySearchField.Title => Contains(entry.Title, term.Value),
                LibrarySearchField.Author => entry.Authors?.Any(author => Contains(author, term.Value)) ?? false,
                LibrarySearchField.Source => Contains(entry.Source, term.Value),
                LibrarySearchField.InternalId => Contains(entry.InternalId, term.Value),
                LibrarySearchField.Doi => Contains(entry.Doi, term.Value),
                LibrarySearchField.Pmid => Contains(entry.Pmid, term.Value),
                LibrarySearchField.Nct => Contains(entry.Nct, term.Value),
                LibrarySearchField.AddedBy => Contains(entry.AddedBy, term.Value),
                LibrarySearchField.Tags => EvaluateTags(entry, term),
                LibrarySearchField.Type => EvaluateTypes(entry, term),
                LibrarySearchField.Year => EvaluateYear(entry, term),
                LibrarySearchField.AddedOn => EvaluateAddedOn(entry, term),
                LibrarySearchField.Internal => EvaluateInternal(entry, term),
                LibrarySearchField.Notes => Contains(entry.Notes, term.Value) || Contains(entry.UserNotes, term.Value),
                _ => EvaluateAny(entry, term.Value)
            };
        }

        private static bool EvaluateTags(Entry entry, LibrarySearchTerm term)
        {
            if (entry.Tags is null || entry.Tags.Count == 0)
            {
                return false;
            }

            if (term.TextValues is { Count: > 0 })
            {
                return term.TextValues.Any(tag => entry.Tags.Any(entryTag => entryTag?.IndexOf(tag, Comparison) >= 0));
            }

            return entry.Tags.Any(tag => Contains(tag, term.Value));
        }

        private static bool EvaluateTypes(Entry entry, LibrarySearchTerm term)
        {
            if (term.TypeValues is { Count: > 0 })
            {
                return term.TypeValues.Contains(entry.Type);
            }

            if (Enum.TryParse<EntryType>(term.Value, true, out var parsed))
            {
                return entry.Type == parsed;
            }

            return false;
        }

        private static bool EvaluateYear(Entry entry, LibrarySearchTerm term)
        {
            if (!entry.Year.HasValue)
            {
                return false;
            }

            var year = entry.Year.Value;
            return term.Operation switch
            {
                LibrarySearchTermOperation.Equals => term.NumberValue.HasValue && year == term.NumberValue.Value,
                LibrarySearchTermOperation.GreaterThan => term.NumberValue.HasValue && year > term.NumberValue.Value,
                LibrarySearchTermOperation.GreaterThanOrEqual => term.NumberValue.HasValue && year >= term.NumberValue.Value,
                LibrarySearchTermOperation.LessThan => term.NumberValue.HasValue && year < term.NumberValue.Value,
                LibrarySearchTermOperation.LessThanOrEqual => term.NumberValue.HasValue && year <= term.NumberValue.Value,
                LibrarySearchTermOperation.Between => term.NumberValue.HasValue && term.SecondaryNumberValue.HasValue &&
                    year >= Math.Min(term.NumberValue.Value, term.SecondaryNumberValue.Value) &&
                    year <= Math.Max(term.NumberValue.Value, term.SecondaryNumberValue.Value),
                _ => Contains(year.ToString(), term.Value)
            };
        }

        private static bool EvaluateAddedOn(Entry entry, LibrarySearchTerm term)
        {
            var date = entry.AddedOnUtc.ToLocalTime().Date;
            return term.Operation switch
            {
                LibrarySearchTermOperation.Equals => term.DateValue.HasValue && date == term.DateValue.Value.Date,
                LibrarySearchTermOperation.GreaterThan => term.DateValue.HasValue && date > term.DateValue.Value.Date,
                LibrarySearchTermOperation.GreaterThanOrEqual => term.DateValue.HasValue && date >= term.DateValue.Value.Date,
                LibrarySearchTermOperation.LessThan => term.DateValue.HasValue && date < term.DateValue.Value.Date,
                LibrarySearchTermOperation.LessThanOrEqual => term.DateValue.HasValue && date <= term.DateValue.Value.Date,
                LibrarySearchTermOperation.Between => term.DateValue.HasValue && term.SecondaryDateValue.HasValue &&
                    date >= term.DateValue.Value.Date && date <= term.SecondaryDateValue.Value.Date,
                _ => Contains(date.ToString("yyyy-MM-dd"), term.Value)
            };
        }

        private static bool EvaluateInternal(Entry entry, LibrarySearchTerm term)
        {
            if (term.BooleanValue.HasValue)
            {
                return entry.IsInternal == term.BooleanValue.Value;
            }

            return term.Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                ? entry.IsInternal
                : !entry.IsInternal;
        }

        private static bool EvaluateAny(Entry entry, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return Contains(entry.Title, value)
                || entry.Authors?.Any(author => Contains(author, value)) == true
                || Contains(entry.Source, value)
                || Contains(entry.InternalId, value)
                || Contains(entry.Doi, value)
                || Contains(entry.Pmid, value)
                || Contains(entry.Nct, value)
                || entry.Tags?.Any(tag => Contains(tag, value)) == true
                || Contains(entry.Notes, value)
                || Contains(entry.UserNotes, value);
        }

        private static bool Contains(string? source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return source.IndexOf(value.Trim(), Comparison) >= 0;
        }
    }
}
