using LoanDocAI.Api.Models;
using LoanDocAI.Api.Services;

namespace LoanDocAI.Tests;

public class IncomeReconciliationServiceTests
{
    private readonly IncomeReconciliationService _service = new();

    [Fact]
    public void Reconcile_approves_when_identity_salary_and_balance_are_valid()
    {
        var slip = new SalarySlipExtraction
        {
            ApplicantName = "Harish Rao",
            EmployerName = "Orchid Textiles Ltd",
            PayMonth = "2026-01",
            GrossSalary = 120000m,
            NetSalary = 100000m
        };

        var statement = new BankStatementExtraction
        {
            ApplicantName = "Harish Rao",
            BankName = "Sahyadri Cooperative Bank",
            StatementFrom = "2026-01-01",
            StatementTo = "2026-01-31",
            OpeningBalance = 25000m,
            ClosingBalance = 35000m,
            TotalCredits = 105000m // Exceeds 100000m
        };

        var result = _service.Reconcile(slip, statement);

        Assert.Equal(VerificationStatus.Approved, result.Status);
        Assert.True(result.ConfidenceScore >= 90);
        Assert.All(result.Checks, c => Assert.True(c.Passed));
    }

    [Fact]
    public void Reconcile_rejects_when_applicant_names_do_not_match()
    {
        var slip = new SalarySlipExtraction
        {
            ApplicantName = "Harish Rao",
            EmployerName = "Orchid Textiles Ltd",
            GrossSalary = 120000m,
            NetSalary = 100000m
        };

        var statement = new BankStatementExtraction
        {
            ApplicantName = "Arjun Mishra", // Different person!
            BankName = "Konkan Savings Bank",
            OpeningBalance = 10000m,
            ClosingBalance = 20000m,
            TotalCredits = 150000m
        };

        var result = _service.Reconcile(slip, statement);

        Assert.Equal(VerificationStatus.Rejected, result.Status);
        Assert.Contains(result.Checks, c => c.CheckName == "Applicant Identity Match" && !c.Passed);
        Assert.Contains("REJECTED", result.SummaryNotes);
    }

    [Fact]
    public void Reconcile_flags_for_review_when_bank_inflows_are_insufficient()
    {
        var slip = new SalarySlipExtraction
        {
            ApplicantName = "Pooja Deshmukh",
            GrossSalary = 150000m,
            NetSalary = 120000m // Declared 1.2 Lakh net
        };

        var statement = new BankStatementExtraction
        {
            ApplicantName = "Pooja Deshmukh",
            OpeningBalance = 5000m,
            ClosingBalance = 6000m,
            TotalCredits = 40000m // Only 40k deposited
        };

        var result = _service.Reconcile(slip, statement);

        Assert.Equal(VerificationStatus.FlaggedForReview, result.Status);
        Assert.Contains(result.Checks, c => c.CheckName == "Deposit Inflow Sufficiency" && !c.Passed);
    }
}
