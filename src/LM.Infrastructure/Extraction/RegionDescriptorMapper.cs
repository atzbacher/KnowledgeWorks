using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using LM.Core.Models;
using Microsoft.Data.Sqlite;

namespace LM.Infrastructure.Extraction
{
    internal static class RegionDescriptorMapper
    {
        private const string ErrorMetadataKey = "last_error_message";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions FileJsonOptions = new(JsonOptions)
        {
            WriteIndented = true
        };

        public static void BindDescriptor(SqliteCommand command, RegionDescriptor descriptor)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));
            if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));

            command.Parameters.AddWithValue("$hash", descriptor.RegionHash);
            command.Parameters.AddWithValue("$entry", descriptor.EntryHubId);
            command.Parameters.AddWithValue("$source", descriptor.SourceRelativePath);
            command.Parameters.AddWithValue("$sourceSha", (object?)descriptor.SourceSha256 ?? DBNull.Value);
            command.Parameters.AddWithValue("$page", descriptor.PageNumber.HasValue ? descriptor.PageNumber.Value : DBNull.Value);
            command.Parameters.AddWithValue("$bounds", SerializeBounds(descriptor.Bounds));
            command.Parameters.AddWithValue("$ocr", (object?)descriptor.OcrText ?? DBNull.Value);
            command.Parameters.AddWithValue("$tags", (object?)SerializeTags(descriptor.Tags) ?? DBNull.Value);
            command.Parameters.AddWithValue("$notes", (object?)descriptor.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$annotation", (object?)descriptor.Annotation ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", FormatDateTime(descriptor.CreatedUtc));
            command.Parameters.AddWithValue("$updated", descriptor.UpdatedUtc is null ? DBNull.Value : FormatDateTime(descriptor.UpdatedUtc.Value));
            command.Parameters.AddWithValue("$status", descriptor.LastExportStatus.ToString());
            command.Parameters.AddWithValue("$error", (object?)GetErrorMessage(descriptor) ?? DBNull.Value);
            command.Parameters.AddWithValue("$office", (object?)descriptor.OfficePackagePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$image", (object?)descriptor.ImagePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$ocrPath", (object?)descriptor.OcrTextPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$exporter", (object?)descriptor.ExporterId ?? DBNull.Value);
            command.Parameters.AddWithValue("$metadata", (object?)SerializeMetadata(descriptor.ExtraMetadata) ?? DBNull.Value);
        }

        public static RegionDescriptor ReadDescriptor(SqliteDataReader reader)
        {
            if (reader is null) throw new ArgumentNullException(nameof(reader));

            var descriptor = new RegionDescriptor
            {
                RegionHash = reader.GetString(reader.GetOrdinal("region_hash")),
                EntryHubId = reader.GetString(reader.GetOrdinal("entry_hub_id")),
                SourceRelativePath = reader.GetString(reader.GetOrdinal("source_rel_path")),
                SourceSha256 = reader.IsDBNull(reader.GetOrdinal("source_sha256")) ? null : reader.GetString(reader.GetOrdinal("source_sha256")),
                PageNumber = reader.IsDBNull(reader.GetOrdinal("page_number")) ? null : reader.GetInt32(reader.GetOrdinal("page_number")),
                Bounds = DeserializeBounds(reader.IsDBNull(reader.GetOrdinal("bounds")) ? null : reader.GetString(reader.GetOrdinal("bounds"))),
                OcrText = reader.IsDBNull(reader.GetOrdinal("ocr_text")) ? null : reader.GetString(reader.GetOrdinal("ocr_text")),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                Annotation = reader.IsDBNull(reader.GetOrdinal("annotation")) ? null : reader.GetString(reader.GetOrdinal("annotation")),
                CreatedUtc = ParseDateTime(reader.GetString(reader.GetOrdinal("created_utc"))),
                UpdatedUtc = reader.IsDBNull(reader.GetOrdinal("updated_utc")) ? null : ParseDateTime(reader.GetString(reader.GetOrdinal("updated_utc"))),
                LastExportStatus = ParseStatus(reader.GetString(reader.GetOrdinal("last_export_status"))),
                OfficePackagePath = reader.IsDBNull(reader.GetOrdinal("office_package_path")) ? null : reader.GetString(reader.GetOrdinal("office_package_path")),
                ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
                OcrTextPath = reader.IsDBNull(reader.GetOrdinal("ocr_text_path")) ? null : reader.GetString(reader.GetOrdinal("ocr_text_path")),
                ExporterId = reader.IsDBNull(reader.GetOrdinal("exporter_id")) ? null : reader.GetString(reader.GetOrdinal("exporter_id"))
            };

            foreach (var tag in DeserializeTags(reader.IsDBNull(reader.GetOrdinal("tags")) ? null : reader.GetString(reader.GetOrdinal("tags"))))
                descriptor.Tags.Add(tag);

            foreach (var kv in DeserializeDictionary(reader.IsDBNull(reader.GetOrdinal("extra_metadata")) ? null : reader.GetString(reader.GetOrdinal("extra_metadata"))))
                descriptor.ExtraMetadata[kv.Key] = kv.Value;

            var error = reader.IsDBNull(reader.GetOrdinal("last_error_message")) ? null : reader.GetString(reader.GetOrdinal("last_error_message"));
            if (!string.IsNullOrWhiteSpace(error))
                descriptor.ExtraMetadata[ErrorMetadataKey] = error;

            return descriptor;
        }

        public static RegionExportResult ReadExportResult(SqliteDataReader reader)
        {
            var descriptor = ReadDescriptor(reader);
            var result = new RegionExportResult
            {
                Descriptor = descriptor,
                ExporterId = descriptor.ExporterId ?? string.Empty,
                ImagePath = descriptor.ImagePath ?? string.Empty,
                OcrTextPath = descriptor.OcrTextPath,
                OfficePackagePath = descriptor.OfficePackagePath,
                WasCached = reader.TryGetBoolean("session_was_cached") ?? false,
                Duration = TimeSpan.FromMilliseconds(reader.TryGetInt64("session_duration_ms") ?? 0),
                CompletedUtc = reader.TryGetDateTime("session_completed_utc") ?? descriptor.UpdatedUtc ?? descriptor.CreatedUtc
            };

            foreach (var kv in DeserializeDictionary(reader.TryGetString("session_additional_outputs")))
                result.AdditionalOutputs[kv.Key] = kv.Value;

            return result;
        }

        public static string FormatDateTime(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        }

        public static string SerializeDescriptor(RegionDescriptor descriptor)
        {
            if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
            return JsonSerializer.Serialize(descriptor, FileJsonOptions);
        }

        public static string? SerializeAdditionalOutputs(IReadOnlyDictionary<string, string> outputs)
        {
            if (outputs is null || outputs.Count == 0)
                return null;
            return JsonSerializer.Serialize(outputs, JsonOptions);
        }

        public static IEnumerable<string> DeserializeTags(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) yield break;
            try
            {
                var tags = JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    yield return tag.Trim();
                }
            }
            catch
            {
                yield break;
            }
        }

        public static IDictionary<string, string> DeserializeDictionary(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                       ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static DateTime ParseDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.UtcNow;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                return dt;
            return DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        }

        private static RegionBounds DeserializeBounds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new RegionBounds();
            try
            {
                var values = JsonSerializer.Deserialize<double[]>(json, JsonOptions);
                if (values is { Length: 4 })
                {
                    return new RegionBounds
                    {
                        X = values[0],
                        Y = values[1],
                        Width = values[2],
                        Height = values[3]
                    };
                }
            }
            catch
            {
                // fall through
            }
            return new RegionBounds();
        }

        private static string SerializeBounds(RegionBounds? bounds)
        {
            bounds ??= new RegionBounds();
            var data = new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height };
            return JsonSerializer.Serialize(data, JsonOptions);
        }

        private static string? SerializeTags(IReadOnlyCollection<string> tags)
        {
            if (tags is not { Count: > 0 })
                return null;

            var normalized = tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
                return null;

            return JsonSerializer.Serialize(normalized, JsonOptions);
        }

        private static string? SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
        {
            if (metadata is null || metadata.Count == 0)
                return null;

            var filtered = metadata
                .Where(kv => !string.Equals(kv.Key, ErrorMetadataKey, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            return filtered.Count == 0 ? null : JsonSerializer.Serialize(filtered, JsonOptions);
        }

        private static RegionExportStatus ParseStatus(string value)
        {
            if (Enum.TryParse<RegionExportStatus>(value, ignoreCase: true, out var status))
                return status;
            return RegionExportStatus.Unknown;
        }

        private static string? GetErrorMessage(RegionDescriptor descriptor)
        {
            if (descriptor.ExtraMetadata.TryGetValue(ErrorMetadataKey, out var message) && !string.IsNullOrWhiteSpace(message))
                return message;
            return null;
        }

        private static string? TryGetString(this SqliteDataReader reader, string column)
        {
            var index = reader.GetOrdinal(column);
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        private static long? TryGetInt64(this SqliteDataReader reader, string column)
        {
            var index = reader.GetOrdinal(column);
            return reader.IsDBNull(index) ? null : reader.GetInt64(index);
        }

        private static bool? TryGetBoolean(this SqliteDataReader reader, string column)
        {
            var index = reader.GetOrdinal(column);
            if (reader.IsDBNull(index)) return null;
            var value = reader.GetInt64(index);
            return value != 0;
        }

        private static DateTime? TryGetDateTime(this SqliteDataReader reader, string column)
        {
            var text = reader.TryGetString(column);
            return string.IsNullOrWhiteSpace(text) ? null : ParseDateTime(text);
        }
    }
}
