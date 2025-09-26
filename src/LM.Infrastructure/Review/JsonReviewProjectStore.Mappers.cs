using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using LM.Review.Core.Models;
using LM.Review.Core.Models.Forms;

namespace LM.Infrastructure.Review;

internal sealed partial class JsonReviewProjectStore
{
    private static class FormResponseMapper
    {
        public static FormResponseDocument FromDomain(FormResponse response, JsonSerializerOptions options)
        {
            return new FormResponseDocument
            {
                Id = response.Id,
                ProjectId = response.ProjectId,
                StageId = response.StageId,
                AssignmentId = response.AssignmentId,
                FormId = response.Snapshot.FormId,
                VersionId = response.Snapshot.VersionId,
                CapturedBy = response.Snapshot.CapturedBy,
                CapturedUtc = response.Snapshot.CapturedUtc,
                Values = response.Snapshot.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => JsonSerializer.SerializeToElement(kvp.Value, options))
            };
        }

        public static FormResponse ToDomain(FormResponseDocument doc, JsonSerializerOptions options)
        {
            var elements = doc.Values ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var values = new Dictionary<string, object?>(elements.Count, StringComparer.Ordinal);
            foreach (var (key, element) in elements)
            {
                values[key] = NormalizeElement(element);
            }

            var snapshot = ExtractionFormSnapshot.Create(doc.FormId, doc.VersionId, values, doc.CapturedBy, doc.CapturedUtc);
            return new FormResponse(doc.Id, doc.ProjectId, doc.StageId, doc.AssignmentId, snapshot);
        }

        private static object? NormalizeElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out var integer))
                        return integer;
                    if (element.TryGetDecimal(out var dec))
                        return dec;
                    return element.GetDouble();
                case JsonValueKind.Array:
                {
                    var list = new List<object?>(element.GetArrayLength());
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(NormalizeElement(item));
                    }
                    return list;
                }
                case JsonValueKind.Object:
                {
                    var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = NormalizeElement(property.Value);
                    }
                    return dict;
                }
                default:
                    return element.GetRawText();
            }
        }
    }
}
