using UglyToad.PdfPig;

namespace LoanDocAI.Api.Services;

public record ExtractedPdfContent(
    IReadOnlyList<string> PageTexts,
    string FullText,
    int PageCount
);

public interface IPdfTextExtractor
{
    ExtractedPdfContent ExtractText(Stream pdfStream);
    ExtractedPdfContent ExtractText(byte[] pdfBytes);
}

public sealed class PdfTextExtractor : IPdfTextExtractor
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB limit

    public ExtractedPdfContent ExtractText(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        if (pdfStream.CanSeek && pdfStream.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size exceeds the 5 MB limit (Actual: {pdfStream.Length} bytes).", nameof(pdfStream));
        }

        using var memoryStream = new MemoryStream();
        pdfStream.CopyTo(memoryStream);
        return ExtractText(memoryStream.ToArray());
    }

    public ExtractedPdfContent ExtractText(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (pdfBytes.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size exceeds the 5 MB limit (Actual: {pdfBytes.Length} bytes).", nameof(pdfBytes));
        }

        using var document = PdfDocument.Open(pdfBytes);
        var pageTexts = new List<string>();

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().ToList();
            if (words.Count > 0)
            {
                // Group words into lines by vertical height, top to bottom
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                    .OrderByDescending(g => g.Key)
                    .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

                pageTexts.Add(string.Join("\n", lines));
            }
            else
            {
                pageTexts.Add(page.Text);
            }
        }

        var fullText = string.Join("\n--- PAGE BREAK ---\n", pageTexts);
        return new ExtractedPdfContent(pageTexts, fullText, document.NumberOfPages);
    }
}
