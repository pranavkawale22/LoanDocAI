using System.Globalization;
using System.Text.RegularExpressions;
using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Services;

public sealed class PatternDocumentExtractor(
    IPdfTextExtractor textExtractor,
    IDocumentClassifier classifier) : IDocumentExtractor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public Task<DocumentExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var content = textExtractor.ExtractText(pdfStream);
        return Task.FromResult(ExtractFromContent(content));
    }

    public Task<DocumentExtractionResult> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var content = textExtractor.ExtractText(pdfBytes);
        return Task.FromResult(ExtractFromContent(content));
    }

    public DocumentExtractionResult ExtractFromContent(ExtractedPdfContent content)
    {
        var docType = classifier.Classify(content);

        return docType switch
        {
            DocumentTypes.SalarySlip => ExtractSalarySlip(content),
            DocumentTypes.BankStatement => ExtractBankStatement(content),
            _ => throw new InvalidOperationException($"Could not determine document type: {docType}")
        };
    }

    private static SalarySlipExtraction ExtractSalarySlip(ExtractedPdfContent content)
    {
        var fullText = content.FullText;
        var lines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 1. Employer Name
        string? employerName = null;
        if (lines.Length > 0)
        {
            if (lines[0].Equals("PAYSLIP", StringComparison.OrdinalIgnoreCase) && lines.Length > 1)
            {
                employerName = lines[1];
            }
            else
            {
                employerName = lines[0];
            }
        }

        // 2. Applicant Name
        string? applicantName = MatchFirst(fullText,
            [
                @"Employee Name:\s*([^\n\r]+)",
                @"Name of Employee:\s*([^\n\r]+)",
                @"Name:\s*([^\n\r]+)"
            ]);

        // 3. Pay Month (YYYY-MM)
        string? payMonth = null;
        var monthMatch = Regex.Match(fullText, @"Salary Slip for ([A-Za-z]+)\s+(\d{4})", RegexOptions.IgnoreCase);
        if (monthMatch.Success)
        {
            if (DateTime.TryParseExact($"{monthMatch.Groups[1].Value} {monthMatch.Groups[2].Value}", "MMMM yyyy", Inv, DateTimeStyles.None, out var dt))
            {
                payMonth = dt.ToString("yyyy-MM", Inv);
            }
        }

        if (payMonth is null)
        {
            var slashMatch = Regex.Match(fullText, @"Pay Month:\s*(\d{2})/(\d{4})", RegexOptions.IgnoreCase);
            if (slashMatch.Success)
            {
                payMonth = $"{slashMatch.Groups[2].Value}-{slashMatch.Groups[1].Value}";
            }
        }

        if (payMonth is null)
        {
            var hyphenMatch = Regex.Match(fullText, @"Pay Slip\s*-\s*([A-Za-z]{3})-(\d{4})", RegexOptions.IgnoreCase);
            if (hyphenMatch.Success)
            {
                if (DateTime.TryParseExact($"{hyphenMatch.Groups[1].Value}-{hyphenMatch.Groups[2].Value}", "MMM-yyyy", Inv, DateTimeStyles.None, out var dt))
                {
                    payMonth = dt.ToString("yyyy-MM", Inv);
                }
            }
        }

        // 4. Gross Salary
        decimal? grossSalary = MatchMoney(fullText,
            [
                @"Gross Earnings\s+(.*?)(?=\s+Total Deductions|$)",
                @"Total Earnings\s*([^\n\r]+)",
                @"Gross Pay\s*([^\n\r]+)"
            ]);

        // 5. Net Salary
        decimal? netSalary = MatchMoney(fullText,
            [
                @"Net Pay:\s*([^\n\r]+)",
                @"Net Salary:\s*([^\n\r]+)",
                @"Take Home:?\s*([^\n\r]+)"
            ]);

        return new SalarySlipExtraction
        {
            ApplicantName = CleanString(applicantName),
            EmployerName = CleanString(employerName),
            PayMonth = payMonth,
            GrossSalary = grossSalary,
            NetSalary = netSalary
        };
    }

    private static BankStatementExtraction ExtractBankStatement(ExtractedPdfContent content)
    {
        var fullText = content.FullText;
        var lines = fullText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // 1. Bank Name
        string? bankName = lines.Length > 0 ? lines[0] : null;

        // 2. Applicant Name
        string? applicantName = MatchFirst(fullText,
            [
                @"Account Holder:\s*([^\n\r]+)",
                @"Customer Name:\s*([^\n\r]+)",
                @"A/c Holder:\s*([^\n\r]+)"
            ]);

        // 3. Statement From & To
        string? statementFrom = null;
        string? statementTo = null;

        var periodMatch = Regex.Match(fullText, @"^\s*Statement Period:\s*([^\n\r]+?)\s+to\s+([^\n\r]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!periodMatch.Success)
        {
            periodMatch = Regex.Match(fullText, @"^\s*Period:\s*([^\n\r]+?)\s+-\s+([^\n\r]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }
        if (!periodMatch.Success)
        {
            periodMatch = Regex.Match(fullText, @"^\s*From:\s*([^\n\r]+?)\s+To\s+([^\n\r]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        if (periodMatch.Success)
        {
            statementFrom = ParseDateToIso(periodMatch.Groups[1].Value.Trim());
            statementTo = ParseDateToIso(periodMatch.Groups[2].Value.Trim());
        }

        // 4. Opening Balance
        decimal? openingBalance = MatchMoney(fullText,
            [
                @"Opening Balance:\s*([^\n\r]+)",
                @"Opening Bal:\s*([^\n\r]+)",
                @"Opening Balance b/f[^\n\r]*?([₹RsINR\d., ]+)(?:\s+Cr)?"
            ]);

        // 5. Total Credits / Deposits
        decimal? totalCredits = MatchMoney(fullText,
            [
                @"Total Credits:\s*([^\n\r]+)",
                @"Total Deposits:\s*([^\n\r]+)",
                @"Total Credit Amount:\s*([^\n\r]+)"
            ]);

        // 6. Closing Balance
        decimal? closingBalance = MatchMoney(fullText,
            [
                @"Closing Balance:\s*([^\n\r]+)",
                @"Closing Bal:\s*([^\n\r]+)",
                @"Closing Balance c/f:\s*([^\n\r]+)"
            ]);

        return new BankStatementExtraction
        {
            ApplicantName = CleanString(applicantName),
            BankName = CleanString(bankName),
            StatementFrom = statementFrom,
            StatementTo = statementTo,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            TotalCredits = totalCredits
        };
    }

    private static string? MatchFirst(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var match = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        return null;
    }

    private static decimal? MatchMoney(string text, string[] patterns)
    {
        foreach (var p in patterns)
        {
            var match = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var val = ParseMoney(match.Groups[1].Value);
                if (val.HasValue) return val;
            }
        }
        return null;
    }

    private static decimal? ParseMoney(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var numMatch = Regex.Match(raw, @"[\d,]+(?:\.\d{1,2})?");
        if (!numMatch.Success) return null;

        var clean = numMatch.Value.Replace(",", "");
        if (decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowDecimalPoint, Inv, out var result))
        {
            return Math.Round(result, 2, MidpointRounding.AwayFromZero);
        }
        return null;
    }

    private static string? ParseDateToIso(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();

        string[] formats = ["dd-MMM-yyyy", "dd/MM/yyyy", "MMMM d, yyyy", "yyyy-MM-dd"];
        if (DateTime.TryParseExact(raw, formats, Inv, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-dd", Inv);
        }

        if (DateTime.TryParse(raw, Inv, DateTimeStyles.None, out dt))
        {
            return dt.ToString("yyyy-MM-dd", Inv);
        }

        return null;
    }

    private static string? CleanString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
