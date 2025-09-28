namespace LM.App.Wpf.ViewModels.Library;

internal sealed partial class DataExtractionPlaygroundViewModel
{
    private const string TesseractRegionSnippet = """
    using Tesseract;

    using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
    using var pix = Pix.LoadFromFile(pageImagePath);
    using var region = pix.ClipRectangle(new System.Drawing.Rectangle(left, top, width, height));
    using var page = engine.Process(region, PageSegMode.SingleBlock);

    var tsv = page.GetTsvText(0);
    var rows = tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    // Map TSV columns 11+ into cells and hydrate a DataTable.
    """;

    private const string HybridConfidenceSnippet = """
    var engine = OcrEnginePool.Rent();
    foreach (var cell in detectedCells)
    {
        using var pix = Pix.LoadFromMemory(cell.ImageBytes);
        using var page = engine.Process(pix, PageSegMode.SingleBlock);
        if (page.GetMeanConfidence() * 100 < 85)
        {
            cell.MarkAsLowConfidence();
        }

        cell.Text = page.GetText();
    }
    """;

    private const string AzureLayoutSnippet = """
    using Azure;
    using Azure.AI.FormRecognizer.DocumentAnalysis;

    var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
    AnalyzeDocumentOperation op = await client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, "prebuilt-layout", pdfUri);
    foreach (var table in op.Value.Tables)
    {
        // Map table.Cells into your view model, using table.Cells[i].RowSpan etc.
    }
    """;

    private const string BringYourOwnLlmSnippet = """"
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
    """";
}
