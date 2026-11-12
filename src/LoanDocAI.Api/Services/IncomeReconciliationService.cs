using System.Text.RegularExpressions;
using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Services;

public interface IIncomeReconciliationService
{
    ReconciliationResult Reconcile(SalarySlipExtraction slip, BankStatementExtraction statement);
}

public sealed class IncomeReconciliationService : IIncomeReconciliationService
{
    public ReconciliationResult Reconcile(SalarySlipExtraction slip, BankStatementExtraction statement)
    {
        ArgumentNullException.ThrowIfNull(slip);
        ArgumentNullException.ThrowIfNull(statement);

        var checks = new List<CheckItem>();
        int passedCount = 0;
        int totalChecks = 4;

        // 1. Applicant Identity Match
        var nameCheck = CheckIdentity(slip.ApplicantName, statement.ApplicantName);
        checks.Add(nameCheck);
        if (nameCheck.Passed) passedCount++;

        // 2. Positive Net Income
        var positiveSalary = slip.NetSalary > 0;
        checks.Add(new CheckItem(
            CheckName: "Positive Net Salary",
            Passed: positiveSalary,
            Details: positiveSalary
                ? $"Declared Net Salary is positive (₹{slip.NetSalary:N2})."
                : "Declared Net Salary is zero or negative."
        ));
        if (positiveSalary) passedCount++;

        // 3. Bank Inflow Sufficiency (Total Credits >= Net Salary)
        bool inflowSufficient = statement.TotalCredits >= slip.NetSalary;
        checks.Add(new CheckItem(
            CheckName: "Deposit Inflow Sufficiency",
            Passed: inflowSufficient,
            Details: inflowSufficient
                ? $"Monthly bank credits (₹{statement.TotalCredits:N2}) adequately cover net salary (₹{slip.NetSalary:N2})."
                : $"Warning: Total credits in statement (₹{statement.TotalCredits:N2}) are lower than declared net salary (₹{slip.NetSalary:N2})."
        ));
        if (inflowSufficient) passedCount++;

        // 4. Financial Health (Non-negative Closing Balance)
        bool healthyBalance = statement.ClosingBalance >= 0;
        checks.Add(new CheckItem(
            CheckName: "Account Solvency",
            Passed: healthyBalance,
            Details: healthyBalance
                ? $"Positive closing balance maintained (₹{statement.ClosingBalance:N2})."
                : $"Alert: Negative closing balance / overdraft detected (₹{statement.ClosingBalance:N2})."
        ));
        if (healthyBalance) passedCount++;

        // Compute overall verdict and confidence
        VerificationStatus status;
        int confidenceScore;
        string summary;

        if (!nameCheck.Passed)
        {
            status = VerificationStatus.Rejected;
            confidenceScore = 25;
            summary = "REJECTED: Critical mismatch between Salary Slip applicant name and Bank Statement account holder.";
        }
        else if (passedCount == totalChecks)
        {
            status = VerificationStatus.Approved;
            confidenceScore = 95;
            summary = "APPROVED: Identity verified, salary validated against bank inflows, and healthy account solvency confirmed.";
        }
        else
        {
            status = VerificationStatus.FlaggedForReview;
            confidenceScore = 65;
            summary = "FLAGGED FOR REVIEW: Identity matches, but variance detected in bank inflows or account balance. Underwriter review recommended.";
        }

        return new ReconciliationResult(status, confidenceScore, checks, summary);
    }

    private static CheckItem CheckIdentity(string? slipName, string? statementName)
    {
        if (string.IsNullOrWhiteSpace(slipName) || string.IsNullOrWhiteSpace(statementName))
        {
            return new CheckItem(
                CheckName: "Applicant Identity Match",
                Passed: false,
                Details: "Missing applicant name on one or both documents."
            );
        }

        var cleanSlip = NormalizeName(slipName);
        var cleanStmt = NormalizeName(statementName);

        if (cleanSlip == cleanStmt)
        {
            return new CheckItem(
                CheckName: "Applicant Identity Match",
                Passed: true,
                Details: $"Names match exactly: '{slipName}'."
            );
        }

        // Check if token sets overlap (handles middle name omissions or order swaps)
        var slipTokens = cleanSlip.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stmtTokens = cleanStmt.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (slipTokens.Length > 0 && stmtTokens.Length > 0 &&
            slipTokens.All(stmtTokens.Contains) || stmtTokens.All(slipTokens.Contains))
        {
            return new CheckItem(
                CheckName: "Applicant Identity Match",
                Passed: true,
                Details: $"Names match with acceptable variation: '{slipName}' vs '{statementName}'."
            );
        }

        return new CheckItem(
            CheckName: "Applicant Identity Match",
            Passed: false,
            Details: $"Identity mismatch: Salary slip lists '{slipName}', but Bank statement belongs to '{statementName}'."
        );
    }

    private static string NormalizeName(string name)
    {
        var clean = name.Trim().ToLowerInvariant();
        // Remove common honorifics
        clean = Regex.Replace(clean, @"\b(mr|ms|mrs|dr|shri|smt)\b\.?", "");
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }
}
