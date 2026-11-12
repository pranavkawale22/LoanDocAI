using System.Text.Json.Serialization;

namespace LoanDocAI.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VerificationStatus
{
    Approved,
    FlaggedForReview,
    Rejected
}

public record CheckItem(
    [property: JsonPropertyName("check_name")] string CheckName,
    [property: JsonPropertyName("passed")] bool Passed,
    [property: JsonPropertyName("details")] string Details
);

public record ReconciliationRequest(
    [property: JsonPropertyName("salary_slip")] SalarySlipExtraction SalarySlip,
    [property: JsonPropertyName("bank_statement")] BankStatementExtraction BankStatement
);

public record ReconciliationResult(
    [property: JsonPropertyName("status")] VerificationStatus Status,
    [property: JsonPropertyName("confidence_score")] int ConfidenceScore,
    [property: JsonPropertyName("checks")] IReadOnlyList<CheckItem> Checks,
    [property: JsonPropertyName("summary_notes")] string SummaryNotes
);
