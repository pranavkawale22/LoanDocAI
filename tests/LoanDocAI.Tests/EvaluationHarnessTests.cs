using System.Text.Json;
using LoanDocAI.Api.Evaluation;
using LoanDocAI.Api.Services;
using Xunit.Abstractions;

namespace LoanDocAI.Tests;

public class EvaluationHarnessTests(ITestOutputHelper output)
{
    private static string SyntheticPdfDir =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "synthetic", "pdfs");

    private static string SyntheticTruthDir =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "synthetic", "truth");

    [Fact]
    public async Task Evaluates_all_twenty_synthetic_documents()
    {
        var textExtractor = new PdfTextExtractor();
        var classifier = new DocumentClassifier();
        var extractor = new PatternDocumentExtractor(textExtractor, classifier);

        var truthFiles = Directory.GetFiles(SyntheticTruthDir, "*.json")
            .OrderBy(f => Path.GetFileNameWithoutExtension(f))
            .ToList();

        Assert.Equal(20, truthFiles.Count);

        var evalResults = new List<DocumentEvaluationResult>();

        foreach (var truthPath in truthFiles)
        {
            var docId = Path.GetFileNameWithoutExtension(truthPath);
            var pdfPath = Path.Combine(SyntheticPdfDir, $"{docId}.pdf");

            Assert.True(File.Exists(pdfPath), $"PDF missing for {docId}");

            using var truthDoc = JsonDocument.Parse(File.ReadAllText(truthPath));
            var pdfBytes = File.ReadAllBytes(pdfPath);

            var extraction = await extractor.ExtractAsync(pdfBytes);
            var result = EvaluationHarness.Evaluate(docId, truthDoc.RootElement, extraction);
            evalResults.Add(result);
        }

        var report = EvaluationHarness.GenerateReport(evalResults);

        using var sw = new StringWriter();
        report.PrintSummary(sw);
        var summary = sw.ToString();

        output.WriteLine(summary);

        Assert.Equal(1.0, report.OverallAccuracy);
    }
}
