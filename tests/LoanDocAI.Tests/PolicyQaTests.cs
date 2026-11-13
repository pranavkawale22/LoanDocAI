using LoanDocAI.Api.Models;
using LoanDocAI.Api.Services;

namespace LoanDocAI.Tests;

public class PolicyQaTests
{
    private readonly PolicyQaService _qaService = new();

    [Fact]
    public async Task Minimum_salary_question_returns_clause_1_1_citation()
    {
        var request = new PolicyQuestionRequest(
            Question: "What is the minimum monthly net salary required for an applicant living in Pune?",
            ApplicantNetSalary: 28000m
        );

        var response = await _qaService.AnswerQuestionAsync(request);

        Assert.Equal(PolicyAnswerStatus.Answered, response.Status);
        Assert.Single(response.Citations);
        Assert.Equal("Clause 1.1", response.Citations[0].ClauseId);
        Assert.Contains("Minimum Monthly Net Income", response.Citations[0].Title);
        Assert.Contains("Tier-2", response.Answer);
        Assert.Contains("ELIGIBLE", response.Answer);
    }

    [Fact]
    public async Task Probation_question_returns_clause_2_2_citation_with_waiver_rules()
    {
        var request = new PolicyQuestionRequest(
            Question: "Can we approve a loan if the applicant is currently on probation?"
        );

        var response = await _qaService.AnswerQuestionAsync(request);

        Assert.Equal(PolicyAnswerStatus.Answered, response.Status);
        Assert.Single(response.Citations);
        Assert.Equal("Clause 2.2", response.Citations[0].ClauseId);
        Assert.Contains("Probation Period Policy", response.Citations[0].Title);
        Assert.Contains("ineligible", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("24 months", response.Answer);
    }

    [Fact]
    public async Task Foir_cap_question_returns_clause_3_1_citation_and_evaluates_salary()
    {
        var request = new PolicyQuestionRequest(
            Question: "What is the maximum permissible FOIR for an applicant?",
            ApplicantNetSalary: 75000m
        );

        var response = await _qaService.AnswerQuestionAsync(request);

        Assert.Equal(PolicyAnswerStatus.Answered, response.Status);
        Assert.Single(response.Citations);
        Assert.Equal("Clause 3.1", response.Citations[0].ClauseId);
        Assert.Contains("60%", response.Answer); // Net salary >= 50k qualifies for 60% FOIR
        Assert.Contains("₹45,000.00", response.Answer); // 60% of 75,000 = 45,000
    }

    [Fact]
    public async Task Out_of_policy_adversarial_question_triggers_not_found_guardrail()
    {
        var request = new PolicyQuestionRequest(
            Question: "Can the borrower pledge Bitcoin or Ethereum cryptocurrency as collateral?"
        );

        var response = await _qaService.AnswerQuestionAsync(request);

        Assert.Equal(PolicyAnswerStatus.NotFound, response.Status);
        Assert.Empty(response.Citations);
        Assert.Equal(0, response.ConfidenceScore);
        Assert.Contains("Information not found", response.Answer);
    }

    [Fact]
    public async Task Irrelevant_query_triggers_not_found_guardrail()
    {
        var request = new PolicyQuestionRequest(
            Question: "What is the best restaurant to visit near the Mumbai office?"
        );

        var response = await _qaService.AnswerQuestionAsync(request);

        Assert.Equal(PolicyAnswerStatus.NotFound, response.Status);
        Assert.Empty(response.Citations);
        Assert.Equal(0, response.ConfidenceScore);
    }
}
