using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.Input;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class DataExtractionPlaygroundViewModel
{
    [RelayCommand]
    private void CopyIdeaSnippet(OcrIdeaViewModel? idea)
    {
        if (idea is null || !idea.HasSnippet)
        {
            return;
        }

        try
        {
            _clipboard.SetText(idea.Snippet);
            StatusMessage = $"Copied {idea.SnippetLabel} for {idea.Title}.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to copy snippet:\n{ex.Message}",
                "Copy idea",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopyIdeaPlan(OcrIdeaViewModel? idea)
    {
        if (idea is null)
        {
            return;
        }

        var plan = idea.BuildPlan();
        if (string.IsNullOrWhiteSpace(plan))
        {
            return;
        }

        try
        {
            _clipboard.SetText(plan);
            StatusMessage = $"Copied plan for {idea.Title}.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to copy plan:\n{ex.Message}",
                "Copy idea",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenIdeaResource(IdeaResourceViewModel? resource)
    {
        if (resource is null || !resource.HasUri)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(resource.Uri!.AbsoluteUri)
            {
                UseShellExecute = true
            };

            Process.Start(startInfo);
            StatusMessage = $"Opened {resource.Label}.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open {resource.Label}:\n{ex.Message}",
                "Open link",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void PopulateIdeaBoard()
    {
        IdeaGroups.Clear();

        var tesseractIdeas = new OcrIdeaGroupViewModel(
            "Tesseract-first experiments",
            "Use offline OCR to complement Tabula when tables are scanned or poorly tagged.",
            new[]
            {
                new OcrIdeaViewModel(
                    "Interactive region OCR",
                    "Allow analysts to drag a rectangle over the preview and run that fragment through Tesseract.",
                    new[]
                    {
                        "Rasterize the selected PDF page into a bitmap (PdfPig's rendering API or SkiaSharp).",
                        "Capture the rectangle in page coordinates and crop the bitmap before OCR.",
                        "Call Tesseract with `PageSegMode.SingleTable` to preserve tab stops.",
                        "Split the TSV output into rows and feed a new `DataExtractionTableViewModel`."
                    },
                    """
using Tesseract;

using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
using var pix = Pix.LoadFromFile(pageImagePath);
using var region = pix.ClipRectangle(new System.Drawing.Rectangle(left, top, width, height));
using var page = engine.Process(region, PageSegMode.SingleTable);

var tsv = page.GetTsvText();
var rows = tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
// Map TSV columns 11+ into cells and hydrate a DataTable.
                    """,
                    "Copy Tesseract region sample",
                    new[] { "Offline", "Tables", "Interactive" },
                    new[]
                    {
                        new IdeaResourceViewModel("Tesseract docs", "https://tesseract-ocr.github.io/tessdoc/"),
                        new IdeaResourceViewModel("Tesseract .NET", "https://github.com/charlesw/tesseract")
                    }),
                new OcrIdeaViewModel(
                    "Hybrid lattice + OCR",
                    "Keep Tabula for grid detection but validate each cell with OCR to clean noisy scans.",
                    new[]
                    {
                        "Run the existing lattice detector to get bounding boxes per cell.",
                        "Rasterize only the detected cells and send them through a shared Tesseract engine instance.",
                        "Replace low-confidence cells with OCR text and emit confidence scores to the grid.",
                        "Highlight suspicious cells in the preview with a semi-transparent overlay."
                    },
                    """
var engine = OcrEnginePool.Rent();
foreach (var cell in detectedCells)
{
    using var pix = Pix.LoadFromMemory(cell.ImageBytes);
    using var page = engine.Process(pix, PageSegMode.SingleBlock);
    if (page.TryGetMeanConfidence(out var confidence) && confidence < 85)
    {
        cell.MarkAsLowConfidence();
    }

    cell.Text = page.GetText();
}
                    """,
                    "Copy confidence blending sample",
                    new[] { "Quality", "Confidence", "Automation" },
                    Array.Empty<IdeaResourceViewModel>()),
                new OcrIdeaViewModel(
                    "Train a table-specific language pack",
                    "Fine-tune Tesseract to the typography of recurring reports and share the traineddata in the workspace.",
                    new[]
                    {
                        "Collect cropped cell images plus the ground truth text while analysts correct results.",
                        "Generate box files with jTessBoxEditor and train a custom `.traineddata` file.",
                        "Ship the model under `.knowledgeworks/tessdata` and auto-sync via the library."
                    },
                    null,
                    null,
                    new[] { "Customization", "Model", "Shared" },
                    new[]
                    {
                        new IdeaResourceViewModel("Training walkthrough", "https://github.com/tesseract-ocr/tesseract/wiki/TrainingTesseract-5")
                    })
            });

        var cloudIdeas = new OcrIdeaGroupViewModel(
            "Cloud document intelligence",
            "Tap managed OCR/AI services when latency and network policies allow it.",
            new[]
            {
                new OcrIdeaViewModel(
                    "Azure Document Intelligence tables",
                    "Send pages to the prebuilt layout model and stream back structured tables with coordinates.",
                    new[]
                    {
                        "Use `DocumentAnalysisClient` with a SAS token sourced from the hub.",
                        "Cache the analysis JSON inside the entry so replays are instant.",
                        "Convert table spans to `DataExtractionTableViewModel` while preserving row/column spans.",
                        "Fallback to Tabula when the service is offline or rate-limited."
                    },
                    """
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;

var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
AnalyzeDocumentOperation op = await client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, "prebuilt-layout", pdfUri);
foreach (var table in op.Value.Tables)
{
    // Map table.Cells into your view model, using table.Cells[i].RowSpan etc.
}
                    """,
                    "Copy Azure layout sample",
                    new[] { "Azure", "Managed", "JSON" },
                    new[]
                    {
                        new IdeaResourceViewModel("Azure setup", "https://learn.microsoft.com/azure/ai-services/document-intelligence/overview"),
                        new IdeaResourceViewModel("SDK docs", "https://learn.microsoft.com/dotnet/api/azure.ai.formrecognizer")
                    }),
                new OcrIdeaViewModel(
                    "AWS Textract for forms",
                    "Leverage Textract AnalyzeDocument to pull tables and key-value pairs simultaneously.",
                    new[]
                    {
                        "Stream PDF bytes via S3 or byte[] to Textract's async API.",
                        "Persist job IDs on the entry hook and poll status with exponential backoff.",
                        "Use the geometry metadata to anchor extracted values back onto the preview overlay."
                    },
                    null,
                    null,
                    new[] { "AWS", "Async", "Overlays" },
                    new[]
                    {
                        new IdeaResourceViewModel("Textract tables", "https://docs.aws.amazon.com/textract/latest/dg/how-it-works-tables.html")
                    }),
                new OcrIdeaViewModel(
                    "Bring-your-own LLM post-check",
                    "Send the raw OCR grid to a hosted LLM to normalize headers, units, and totals.",
                    new[]
                    {
                        "Compose a prompt with the TSV payload and ask for JSON-formatted corrections.",
                        "Validate numeric totals locally before applying changes.",
                        "Log adjustments through the changelog hook for traceability."
                    },
                    """
var prompt = $$"""
You are cleaning an OCR table. Return strict JSON with fields rows, warnings.

{tsv}
"""$$;

var response = await openAiClient.GetChatCompletionsAsync(deploymentId, new ChatCompletionsOptions
{
    Messages =
    {
        new ChatRequestSystemMessage("Normalize table headers and units"),
        new ChatRequestUserMessage(prompt)
    }
});
                    """,
                    "Copy LLM normalization sample",
                    new[] { "LLM", "Validation", "Normalization" },
                    Array.Empty<IdeaResourceViewModel>())
            });

        var automationIdeas = new OcrIdeaGroupViewModel(
            "Automation & QA",
            "Wrap OCR with tooling so analysts can trust the results.",
            new[]
            {
                new OcrIdeaViewModel(
                    "Versioned extraction recipes",
                    "Store playground presets (mode, detector, OCR toggles) in the workspace so teams can replay them.",
                    new[]
                    {
                        "Serialize the current playground settings into YAML alongside the entry.",
                        "Add a toolbar to load/save recipes and compare outputs with diff tooling.",
                        "Write every replay to the changelog hook with the recipe identifier."
                    },
                    null,
                    null,
                    new[] { "Repeatable", "Presets", "Collaboration" },
                    Array.Empty<IdeaResourceViewModel>()),
                new OcrIdeaViewModel(
                    "Confidence heatmaps",
                    "Overlay a color-coded heatmap on the PDF preview based on OCR confidence and Tabula heuristics.",
                    new[]
                    {
                        "Project OCR bounding boxes back to PDF coordinates.",
                        "Blend them over the WebBrowser host using a transparent Canvas.",
                        "Expose filters to hide low-risk areas so reviewers focus on the outliers."
                    },
                    null,
                    null,
                    new[] { "UX", "Visualization", "QA" },
                    Array.Empty<IdeaResourceViewModel>()),
                new OcrIdeaViewModel(
                    "Automated reconciliation",
                    "Compare extracted totals against ledger data or previously ingested entries to flag drifts.",
                    new[]
                    {
                        "Cross-match numeric columns with known control totals from the knowledge graph.",
                        "Trigger a review task when deltas exceed configured tolerances.",
                        "Persist the reconciliation result back into the hook metadata for auditing."
                    },
                    null,
                    null,
                    new[] { "Controls", "Monitoring", "Hooks" },
                    Array.Empty<IdeaResourceViewModel>())
            });

        IdeaGroups.Add(tesseractIdeas);
        IdeaGroups.Add(cloudIdeas);
        IdeaGroups.Add(automationIdeas);

        OnPropertyChanged(nameof(HasIdeaGroups));
    }
}

internal sealed class OcrIdeaGroupViewModel
{
    public OcrIdeaGroupViewModel(string title, string description, IEnumerable<OcrIdeaViewModel> ideas)
    {
        Title = title;
        Description = description;
        Ideas = new ObservableCollection<OcrIdeaViewModel>((ideas ?? Array.Empty<OcrIdeaViewModel>()).ToList());
    }

    public string Title { get; }

    public string Description { get; }

    public ObservableCollection<OcrIdeaViewModel> Ideas { get; }
}

internal sealed class OcrIdeaViewModel
{
    private readonly string? _snippetLabel;

    public OcrIdeaViewModel(string title,
                            string summary,
                            IEnumerable<string> steps,
                            string? snippet,
                            string? snippetLabel,
                            IEnumerable<string>? tags,
                            IEnumerable<IdeaResourceViewModel>? resources)
    {
        Title = title;
        Summary = summary;

        var stepList = steps?.Where(step => !string.IsNullOrWhiteSpace(step)).Select(step => step.Trim()).ToList()
                       ?? new List<string>();
        Steps = new ReadOnlyCollection<string>(stepList);

        Snippet = snippet?.TrimEnd() ?? string.Empty;
        _snippetLabel = snippetLabel;

        var tagList = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToList()
                     ?? new List<string>();
        Tags = new ReadOnlyCollection<string>(tagList);

        var resourceList = resources?.Where(resource => resource is not null).ToList()
                          ?? new List<IdeaResourceViewModel>();
        Resources = new ObservableCollection<IdeaResourceViewModel>(resourceList);
    }

    public string Title { get; }

    public string Summary { get; }

    public ReadOnlyCollection<string> Steps { get; }

    public string Snippet { get; }

    public bool HasSnippet => !string.IsNullOrWhiteSpace(Snippet);

    public bool HasSteps => Steps.Count > 0;

    public string SnippetLabel => string.IsNullOrWhiteSpace(_snippetLabel) ? "Copy sample code" : _snippetLabel!;

    public ReadOnlyCollection<string> Tags { get; }

    public ObservableCollection<IdeaResourceViewModel> Resources { get; }

    public bool HasResources => Resources.Count > 0;

    public bool HasTags => Tags.Count > 0;

    public string BuildPlan()
    {
        var builder = new StringBuilder();
        builder.AppendLine(Title);

        if (!string.IsNullOrWhiteSpace(Summary))
        {
            builder.AppendLine(Summary);
        }

        if (Steps.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Execution checklist:");
            foreach (var step in Steps)
            {
                builder.AppendLine($"- {step}");
            }
        }

        if (Tags.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Tags: {string.Join(", ", Tags)}");
        }

        return builder.ToString().Trim();
    }
}

internal sealed class IdeaResourceViewModel
{
    public IdeaResourceViewModel(string label, string url)
    {
        Label = label;
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            Uri = parsed;
        }
    }

    public string Label { get; }

    public Uri? Uri { get; }

    public bool HasUri => Uri is not null;
}

