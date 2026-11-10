using LoanDocAI.Api.Services;

namespace LoanDocAI.Tests;

public class PdfExtractorTests
{
    private static string SyntheticPdfDir =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "synthetic", "pdfs");

    [Fact]
    public void Extracts_text_from_salary_slip_correctly()
    {
        var extractor = new PdfTextExtractor();
        var slipPath = Path.Combine(SyntheticPdfDir, "slip-01.pdf");
        Assert.True(File.Exists(slipPath), $"File not found at {slipPath}");

        var bytes = File.ReadAllBytes(slipPath);
        var content = extractor.ExtractText(bytes);

        Assert.True(content.PageCount >= 1);
        Assert.NotEmpty(content.FullText);
        // From truth: applicant is Harish Rao, employer is Orchid Textiles Ltd
        Assert.Contains("Harish Rao", content.FullText);
        Assert.Contains("Orchid Textiles Ltd", content.FullText);
    }

    [Fact]
    public void Extracts_text_from_bank_statement_correctly()
    {
        var extractor = new PdfTextExtractor();
        var stmtPath = Path.Combine(SyntheticPdfDir, "stmt-01.pdf");
        Assert.True(File.Exists(stmtPath), $"File not found at {stmtPath}");

        var bytes = File.ReadAllBytes(stmtPath);
        var content = extractor.ExtractText(bytes);

        Assert.True(content.PageCount >= 1);
        Assert.NotEmpty(content.FullText);
        // From truth: applicant is Arjun Mishra, bank is Konkan Savings Bank
        Assert.Contains("Arjun Mishra", content.FullText);
        Assert.Contains("Konkan Savings Bank", content.FullText);
    }

    [Fact]
    public void Throws_when_file_exceeds_5mb()
    {
        var extractor = new PdfTextExtractor();
        var oversized = new byte[PdfTextExtractor.MaxFileSizeBytes + 1];

        Assert.Throws<ArgumentException>(() => extractor.ExtractText(oversized));
    }
}
