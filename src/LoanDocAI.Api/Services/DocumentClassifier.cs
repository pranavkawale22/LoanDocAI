using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Services;

public interface IDocumentClassifier
{
    string Classify(ExtractedPdfContent content);
}

public sealed class DocumentClassifier : IDocumentClassifier
{
    private static readonly string[] SalarySlipKeywords =
    [
        "salary slip", "pay slip", "payslip", "gross salary", "gross earnings", "gross pay",
        "net salary", "net pay", "take home", "deductions", "total deductions", "emp code",
        "employee id", "basic pay", "hra"
    ];

    private static readonly string[] BankStatementKeywords =
    [
        "statement of account", "account statement", "opening balance", "closing balance",
        "opening bal", "closing bal", "total credits", "total debits", "total deposits",
        "total withdrawals", "withdrawals", "deposits", "account no", "account holder", "a/c"
    ];

    public string Classify(ExtractedPdfContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lowerText = content.FullText.ToLowerInvariant();

        var slipScore = SalarySlipKeywords.Count(kw => lowerText.Contains(kw));
        var stmtScore = BankStatementKeywords.Count(kw => lowerText.Contains(kw));

        if (slipScore > stmtScore && slipScore > 0)
        {
            return DocumentTypes.SalarySlip;
        }

        if (stmtScore > slipScore && stmtScore > 0)
        {
            return DocumentTypes.BankStatement;
        }

        if (lowerText.Contains("payslip") || lowerText.Contains("salary slip") || lowerText.Contains("pay slip"))
        {
            return DocumentTypes.SalarySlip;
        }

        if (lowerText.Contains("account statement") || lowerText.Contains("statement of account"))
        {
            return DocumentTypes.BankStatement;
        }

        return DocumentTypes.Unknown;
    }
}
