using System.Text.Json.Serialization;

namespace LoanDocAI.Api.Models;

public static class DocumentTypes
{
    public const string SalarySlip = "salary_slip";
    public const string BankStatement = "bank_statement";
    public const string Unknown = "unknown";
}

public abstract record DocumentExtractionResult
{
    [JsonPropertyName("document_type")]
    public abstract string DocumentType { get; }

    [JsonPropertyName("applicant_name")]
    public string? ApplicantName { get; init; }
}

public sealed record SalarySlipExtraction : DocumentExtractionResult
{
    [JsonPropertyName("document_type")]
    public override string DocumentType => DocumentTypes.SalarySlip;

    [JsonPropertyName("employer_name")]
    public string? EmployerName { get; init; }

    [JsonPropertyName("pay_month")]
    public string? PayMonth { get; init; }

    [JsonPropertyName("gross_salary")]
    public decimal? GrossSalary { get; init; }

    [JsonPropertyName("net_salary")]
    public decimal? NetSalary { get; init; }
}

public sealed record BankStatementExtraction : DocumentExtractionResult
{
    [JsonPropertyName("document_type")]
    public override string DocumentType => DocumentTypes.BankStatement;

    [JsonPropertyName("bank_name")]
    public string? BankName { get; init; }

    [JsonPropertyName("statement_from")]
    public string? StatementFrom { get; init; }

    [JsonPropertyName("statement_to")]
    public string? StatementTo { get; init; }

    [JsonPropertyName("opening_balance")]
    public decimal? OpeningBalance { get; init; }

    [JsonPropertyName("closing_balance")]
    public decimal? ClosingBalance { get; init; }

    [JsonPropertyName("total_credits")]
    public decimal? TotalCredits { get; init; }
}
