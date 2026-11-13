using System.Text.Json.Serialization;

namespace LoanDocAI.Api.Models;

public record PolicyClause(
    [property: JsonPropertyName("clause_id")] string ClauseId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords
);

public record PolicyDocument(
    [property: JsonPropertyName("policy_name")] string PolicyName,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("effective_date")] string EffectiveDate,
    [property: JsonPropertyName("clauses")] IReadOnlyList<PolicyClause> Clauses
);

public record PolicyQuestionRequest(
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("applicant_net_salary")] decimal? ApplicantNetSalary = null,
    [property: JsonPropertyName("city")] string? City = null
);

public record PolicyCitation(
    [property: JsonPropertyName("clause_id")] string ClauseId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("excerpt")] string Excerpt
);

public static class PolicyAnswerStatus
{
    public const string Answered = "Answered";
    public const string NotFound = "NotFound";
}

public record PolicyAnswerResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("citations")] IReadOnlyList<PolicyCitation> Citations,
    [property: JsonPropertyName("confidence_score")] int ConfidenceScore
);
