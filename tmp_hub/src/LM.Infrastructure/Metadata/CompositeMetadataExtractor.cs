using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using LM.Core.Abstractions;
using LM.Core.Models;
using LM.Infrastructure.Export;    // MetadataDebugSlideExporter
using LM.Infrastructure.Utils;     // TagNormalizer
using UglyToad.PdfPig;

namespace LM.Infrastructure.Metadata
{
    /// <summary>
    /// Robust, best-effort metadata extraction from PDFs, DOCX, PPTX, TXT.
    /// For PDFs: uses PdfPig DocumentInformation AND scans XMP for keywords/doi/title/authors.
    /// Optional debug dump/slide go under workspace/.kw/_debug/metadata when env flags enabled.
    /// </summary>
    public sealed class CompositeMetadataExtractor : IMetadataExtractor
    {
        private readonly IContentExtractor _content;
        public CompositeMetadataExtractor(IContentExtractor content) => _content = content;

        public async Task<FileMetadata> ExtractAsync(string absolutePath, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(absolutePath)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf"              => await FromPdfAsync(absolutePath, ct),
                ".docx" or ".doc"   => FromDocx(absolutePath),
                ".pptx" or ".ppt"   => FromPptx(absolutePath),
                ".txt" or ".md"     => await FromTextAsync(absolutePath, ct),
                _ => new FileMetadata()
            };
        }

        // ---------------- PDF ----------------

        private async Task<FileMetadata> FromPdfAsync(string path, CancellationToken ct)
        {
            var meta = new FileMetadata();
            XDocument? xmp = null;

            // 1) PdfPig DocumentInformation
            try
            {
                using var pdf = PdfDocument.Open(path);
                var info = pdf.Information;

                if (!string.IsNullOrWhiteSpace(info?.Title))
                    meta.Title = info!.Title;

                if (!string.IsNullOrWhiteSpace(info?.Author))
                {
                    meta.Authors = info!.Author!
                        .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => a.Length > 0)
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(info?.Subject))
                {
                    var subj = info!.Subject!.Trim();
                    var doiInSubject = TryFindDoi(subj);
                    if (!string.IsNullOrEmpty(doiInSubject))
                        meta.Doi ??= doiInSubject;
                    else
                        meta.Source ??= subj;
                }

                if (!string.IsNullOrWhiteSpace(info?.Keywords))
                {
                    foreach (var k in TagNormalizer.SplitAndNormalize(info!.Keywords!))
                        if (!meta.Tags.Any(t => string.Equals(t, k, StringComparison.OrdinalIgnoreCase)))
                            meta.Tags.Add(k);
                }

                meta.Year = TryExtractYear(info, "CreationDate") ?? meta.Year;

                // Try to locate XMP (publishers may only populate XMP)
                xmp = TryExtractXmp(path);
            }
            catch
            {
                xmp ??= TryExtractXmp(path);
            }

            // 2) XMP enrichment
            if (xmp is not null) EnrichFromXmp(xmp, meta);

            // 3) Text-based enrichment (DOI/PMID scraping)
            var text = await _content.ExtractTextAsync(path, ct);
            meta.Doi  ??= TryFindDoi(text);
            meta.Pmid ??= TryFindPmid(text);

            // 4) Debug outputs
            await TryWriteDebugDumpAsync(path, meta, xmp, ct);
            await TryExportDebugSlideAsync(meta, path, ct);

            return meta;
        }

        // ---------------- DOCX/PPTX/TXT ----------------

                private FileMetadata FromDocx(string path)
        {
            var meta = new FileMetadata();
            try
            {
                using var doc = WordprocessingDocument.Open(path, false);
                var p = doc.PackageProperties;

                if (!string.IsNullOrWhiteSpace(p.Title))   meta.Title   = p.Title;
                if (!string.IsNullOrWhiteSpace(p.Creator)) meta.Authors = SplitCsv(p.Creator).ToList(); // <-- fix

                meta.Year = TryExtractYear(p, "Created") ?? meta.Year;

                if (!string.IsNullOrWhiteSpace(p.Subject))
                {
                    var doiInSubject = TryFindDoi(p.Subject!);
                    if (!string.IsNullOrWhiteSpace(doiInSubject)) meta.Doi = doiInSubject;
                    else meta.Source ??= p.Subject!.Trim();
                }

                if (!string.IsNullOrWhiteSpace(p.Keywords))
                    foreach (var k in TagNormalizer.SplitAndNormalize(p.Keywords!))
                        if (!meta.Tags.Any(t => string.Equals(t, k, StringComparison.OrdinalIgnoreCase)))
                            meta.Tags.Add(k);
            }
            catch { }
            return meta;
        }

        private FileMetadata FromPptx(string path)
        {
            var meta = new FileMetadata();
            try
            {
                using var pres = PresentationDocument.Open(path, false);
                var p = pres.PackageProperties;

                if (!string.IsNullOrWhiteSpace(p.Title))   meta.Title   = p.Title;
                if (!string.IsNullOrWhiteSpace(p.Creator)) meta.Authors = SplitCsv(p.Creator).ToList(); // <-- fix

                meta.Year = TryExtractYear(p, "Created") ?? meta.Year;

                if (!string.IsNullOrWhiteSpace(p.Subject))
                {
                    var doiInSubject = TryFindDoi(p.Subject!);
                    if (!string.IsNullOrWhiteSpace(doiInSubject)) meta.Doi = doiInSubject;
                    else meta.Source ??= p.Subject!.Trim();
                }

                if (!string.IsNullOrWhiteSpace(p.Keywords))
                    foreach (var k in TagNormalizer.SplitAndNormalize(p.Keywords!))
                        if (!meta.Tags.Any(t => string.Equals(t, k, StringComparison.OrdinalIgnoreCase)))
                            meta.Tags.Add(k);
            }
            catch { }
            return meta;
        }

        private async Task<FileMetadata> FromTextAsync(string path, CancellationToken ct)
        {
            var meta = new FileMetadata();
            var text = await _content.ExtractTextAsync(path, ct);
            meta.Doi  = TryFindDoi(text);
            meta.Pmid = TryFindPmid(text);
            return meta;
        }

        // ---------------- Helpers ----------------

        private static int? TryExtractYear(object? obj, string propName)
        {
            try
            {
                if (obj is null) return null;
                var prop = obj.GetType().GetProperty(propName);
                if (prop is null) return null;
                var val = prop.GetValue(obj);
                if (val is null) return null;

                return val switch
                {
                    DateTimeOffset dto => (int?)dto.Year,
                    DateTime dt        => (int?)dt.Year,
                    string s           => TryParseYear(s),
                    _                  => TryParseYear(val.ToString() ?? "")
                };
            }
            catch { return null; }
        }

        private static int? TryParseYear(string s)
        {
            var m = Regex.Match(s, @"(?:D:)?(?<y>\d{4})");
            return m.Success && int.TryParse(m.Groups["y"].Value, out var y) ? y : null;
        }

        private static string[] SplitCsv(string raw) =>
            raw.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
               .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        private static string? TryFindDoi(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Regex.Match(text, @"\b10\.\d{4,9}/[-._;()/:A-Z0-9]+\b", RegexOptions.IgnoreCase);
            return m.Success ? m.Value : null;
        }

        private static string? TryFindPmid(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Regex.Match(text, @"PMID\s*[:#]?\s*(\d{5,9})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        // -------- XMP --------

        private static XDocument? TryExtractXmp(string pdfPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(pdfPath);
                var s = Encoding.Latin1.GetString(bytes);
                var m = Regex.Match(s, "<x:xmpmeta[\\s\\S]*?</x:xmpmeta>", RegexOptions.IgnoreCase);
                if (!m.Success) return null;
                return XDocument.Parse(m.Value);
            }
            catch { return null; }
        }

        private static void EnrichFromXmp(XDocument xmp, FileMetadata meta)
        {
            try
            {
                XNamespace rdf   = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
                XNamespace dc    = "http://purl.org/dc/elements/1.1/";
                XNamespace pdfNs = "http://ns.adobe.com/pdf/1.3/";
                XNamespace prism = "http://prismstandard.org/namespaces/basic/2.0/";
                XNamespace xmpNs = "http://ns.adobe.com/xap/1.0/";

                // title
                if (string.IsNullOrWhiteSpace(meta.Title))
                {
                    var t = xmp.Descendants(dc + "title").Descendants(rdf + "Alt").Elements(rdf + "li")
                               .Select(e => e.Value?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    if (!string.IsNullOrWhiteSpace(t)) meta.Title = t!;
                }

                // authors
                if (meta.Authors.Count == 0)
                {
                    var authors = xmp.Descendants(dc + "creator").Descendants(rdf + "Seq").Elements(rdf + "li")
                                     .Select(e => e.Value?.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                    if (authors.Count > 0) meta.Authors = authors!;
                }

                // source/journal
                if (string.IsNullOrWhiteSpace(meta.Source))
                {
                    var source = xmp.Descendants(prism + "publicationName").Select(e => e.Value?.Trim())
                                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    if (!string.IsNullOrWhiteSpace(source)) meta.Source = source!;
                }

                // doi
                if (string.IsNullOrWhiteSpace(meta.Doi))
                {
                    var doi = xmp.Descendants(prism + "doi").Select(e => e.Value?.Trim())
                                 .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    doi ??= xmp.Descendants(dc + "identifier").Select(e => TryFindDoi(e.Value)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    if (!string.IsNullOrWhiteSpace(doi)) meta.Doi = doi!;
                }

                // year (CreateDate / publicationDate)
                if (!meta.Year.HasValue)
                {
                    var dtStr = xmp.Descendants(xmpNs + "CreateDate").Select(e => e.Value).FirstOrDefault()
                              ?? xmp.Descendants(prism + "publicationDate").Select(e => e.Value).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(dtStr))
                    {
                        if (DateTimeOffset.TryParse(dtStr, out var dto)) meta.Year = dto.Year;
                        else if (Regex.Match(dtStr, @"(?<y>\d{4})") is Match m && m.Success && int.TryParse(m.Groups["y"].Value, out var y))
                            meta.Year = y;
                    }
                }

                // tags (pdf:Keywords + dc:subject Bag)
                var newTags = Enumerable.Empty<string>();

                var pdfKw = xmp.Descendants(pdfNs + "Keywords").Select(e => e.Value).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(pdfKw))
                    newTags = newTags.Concat(TagNormalizer.SplitAndNormalize(pdfKw));

                var dcBag = xmp.Descendants(dc + "subject").Descendants(rdf + "Bag").Elements(rdf + "li")
                               .Select(e => e.Value?.Trim()).Where(v => !string.IsNullOrWhiteSpace(v));
                newTags = newTags.Concat(dcBag!);

                foreach (var k in newTags)
                    if (!meta.Tags.Any(t => string.Equals(t, k, StringComparison.OrdinalIgnoreCase)))
                        meta.Tags.Add(k);
            }
            catch { /* best-effort */ }
        }

        // -------- Debug outputs --------

        private static bool DebugDumpEnabled() =>
            IsTrue("KW_DEBUG_DUMP") || IsTrue("KW_DEBUG_VERBOSE");

        private static bool SlidesEnabled() => IsTrue("KW_DEBUG_SLIDES");

        private static bool IsTrue(string env) =>
            string.Equals(Environment.GetEnvironmentVariable(env), "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable(env), "true", StringComparison.OrdinalIgnoreCase);

        private static void Verbose(string msg)
        {
            if (!IsTrue("KW_DEBUG_VERBOSE")) return;
            try { Debug.WriteLine(msg); } catch { }
            try { Console.WriteLine(msg); } catch { }
        }

        private static string DebugRoot(string sourcePath)
        {
            var explicitDir = Environment.GetEnvironmentVariable("KW_DEBUG_DIR");
            if (!string.IsNullOrWhiteSpace(explicitDir)) return explicitDir!;
            var ws = Environment.GetEnvironmentVariable("KW_WORKSPACE");
            if (!string.IsNullOrWhiteSpace(ws)) return Path.Combine(ws!, ".kw", "_debug");
            return Path.Combine(Path.GetDirectoryName(sourcePath) ?? ".", "_debug");
        }

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var arr = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            var s = new string(arr);
            return string.IsNullOrWhiteSpace(s) ? "file" : s;
        }

        private static async Task TryWriteDebugDumpAsync(string sourcePath, FileMetadata meta, XDocument? xmp, CancellationToken ct)
        {
            if (!DebugDumpEnabled()) return;
            try
            {
                var root = DebugRoot(sourcePath);
                var dir  = Path.Combine(root, "metadata");
                Directory.CreateDirectory(dir);
                var baseName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
                var outPath  = Path.Combine(dir, $"{baseName}_info.txt");

                var sb = new StringBuilder();
                sb.AppendLine($"Source: {sourcePath}");
                sb.AppendLine($"Written: {DateTimeOffset.Now:O}");
                sb.AppendLine();

                // PdfPig Info reflection
                try
                {
                    using var pdf = PdfDocument.Open(sourcePath);
                    var info = pdf.Information;
                    if (info is not null)
                    {
                        sb.AppendLine("== PdfPig DocumentInformation ==");
                        foreach (var p in info.GetType().GetProperties())
                        {
                            object? val = null;
                            try { val = p.GetValue(info); } catch { }
                            sb.AppendLine($"{p.Name}: {val}");
                        }
                        sb.AppendLine();
                    }
                }
                catch (Exception ex) { sb.AppendLine($"[Info read failed] {ex.Message}\n"); }

                // XMP (raw first 2KB)
                if (xmp is not null)
                {
                    sb.AppendLine("== XMP present ==");
                    var xml = xmp.ToString(SaveOptions.DisableFormatting);
                    sb.AppendLine(xml.Length > 2048 ? xml.Substring(0, 2048) + " ..." : xml);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("== XMP present == NO\n");
                }

                sb.AppendLine("== Extracted FileMetadata ==");
                sb.AppendLine(JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
                sb.AppendLine();

                await File.WriteAllTextAsync(outPath, sb.ToString(), Encoding.UTF8, ct);
                Verbose($"[KW_DEBUG] wrote dump {outPath}");
            }
            catch (Exception ex) { Verbose($"[KW_DEBUG] dump failed: {ex.Message}"); }
        }

        private static async Task TryExportDebugSlideAsync(FileMetadata meta, string sourcePath, CancellationToken ct)
        {
            if (!SlidesEnabled()) return;
            try
            {
                var root = DebugRoot(sourcePath);
                var dir  = Path.Combine(root, "metadata");
                Directory.CreateDirectory(dir);
                var baseName = Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
                var outPath  = Path.Combine(dir, $"{baseName}_meta.pptx");

                var exporter = new MetadataDebugSlideExporter();
                await exporter.ExportAsync(new[] { meta }, outPath, ct);
                Verbose($"[KW_DEBUG] wrote slide {outPath}");
            }
            catch (Exception ex) { Verbose($"[KW_DEBUG] slide failed: {ex.Message}"); }
        }
    }
}
