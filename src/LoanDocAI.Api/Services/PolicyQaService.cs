using System.Text.Json;
using System.Text.RegularExpressions;
using LoanDocAI.Api.Models;

namespace LoanDocAI.Api.Services;

public interface IPolicyQaService
{
    Task<PolicyAnswerResult> AnswerQuestionAsync(PolicyQuestionRequest request, CancellationToken cancellationToken = default);
}

public sealed class PolicyQaService : IPolicyQaService
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "in", "on", "at", "to", "for", "of", "with", "is", "are", "was",
        "were", "can", "could", "should", "would", "do", "does", "what", "how", "why",
        "we", "our", "you", "they", "it", "this", "that", "if", "and", "or", "by"
    ];

    private readonly List<PolicyClause> _clauses = [];
    private string _policyName = "Credit Underwriting Guidelines";

    public PolicyQaService()
    {
        LoadPolicy();
    }

    public PolicyQaService(string policyJson)
    {
        var doc = JsonSerializer.Deserialize<PolicyDocument>(policyJson);
        if (doc is not null)
        {
            _policyName = doc.PolicyName;
            _clauses.AddRange(doc.Clauses);
        }
    }

    private void LoadPolicy()
    {
        var path = FindPolicyPath();
        if (path is not null && File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<PolicyDocument>(json);
            if (doc is not null)
            {
                _policyName = doc.PolicyName;
                _clauses.AddRange(doc.Clauses);
            }
        }
    }

    private static string? FindPolicyPath()
    {
        var current = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(current); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "policies", "credit_policy.json");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    public Task<PolicyAnswerResult> AnswerQuestionAsync(
        PolicyQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Task.FromResult(new PolicyAnswerResult(
                Status: PolicyAnswerStatus.NotFound,
                Answer: "Please provide a valid policy question.",
                Citations: [],
                ConfidenceScore: 0
            ));
        }

        var queryTokens = Tokenize(request.Question);
        if (queryTokens.Count == 0)
        {
            return Task.FromResult(CreateNotFoundResult());
        }

        // Score each clause against query
        var scored = _clauses.Select(c => new
        {
            Clause = c,
            Score = ComputeRelevance(c, request.Question, queryTokens)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        var top = scored.FirstOrDefault();

        // Strict Hallucination Guardrail: If relevance is too low, reject immediately
        const double RelevanceThreshold = 0.30;
        if (top is null || top.Score < RelevanceThreshold)
        {
            return Task.FromResult(CreateNotFoundResult());
        }

        var matchedClause = top.Clause;
        var citations = new List<PolicyCitation>
        {
            new(
                ClauseId: matchedClause.ClauseId,
                Title: matchedClause.Title,
                Excerpt: matchedClause.Content
            )
        };

        // Synthesize grounded answer
        var answer = BuildGroundedAnswer(matchedClause, request);
        int confidence = Math.Clamp((int)(top.Score * 100), 75, 98);

        return Task.FromResult(new PolicyAnswerResult(
            Status: PolicyAnswerStatus.Answered,
            Answer: answer,
            Citations: citations,
            ConfidenceScore: confidence
        ));
    }

    private static PolicyAnswerResult CreateNotFoundResult() => new(
        Status: PolicyAnswerStatus.NotFound,
        Answer: "Information not found in approved credit underwriting policy documentation.",
        Citations: [],
        ConfidenceScore: 0
    );

    private static double ComputeRelevance(PolicyClause clause, string rawQuery, HashSet<string> queryTokens)
    {
        double score = 0.0;
        var lowerQuery = rawQuery.ToLowerInvariant();

        // 1. Keyword direct match (high signal)
        foreach (var kw in clause.Keywords)
        {
            if (lowerQuery.Contains(kw.ToLowerInvariant()))
            {
                score += 0.35;
            }
        }

        // 2. Title token matches
        var titleTokens = Tokenize(clause.Title);
        int titleMatches = queryTokens.Intersect(titleTokens).Count();
        if (titleTokens.Count > 0)
        {
            score += (double)titleMatches / titleTokens.Count * 0.30;
        }

        // 3. Content token matches
        var contentTokens = Tokenize(clause.Content);
        int contentMatches = queryTokens.Intersect(contentTokens).Count();
        if (contentTokens.Count > 0)
        {
            score += (double)contentMatches / queryTokens.Count * 0.25;
        }

        // 4. Domain specific bonuses (City detection, FOIR, Age)
        if (lowerQuery.Contains("pune") || lowerQuery.Contains("mumbai") || lowerQuery.Contains("delhi") || lowerQuery.Contains("metro"))
        {
            if (clause.ClauseId == "Clause 1.1") score += 0.25;
        }

        if (lowerQuery.Contains("foir") || lowerQuery.Contains("obligation") || lowerQuery.Contains("debt"))
        {
            if (clause.ClauseId == "Clause 3.1") score += 0.30;
        }

        if (lowerQuery.Contains("probation") || lowerQuery.Contains("probationary"))
        {
            if (clause.ClauseId == "Clause 2.2") score += 0.40;
        }

        return Math.Min(score, 1.0);
    }

    private static string BuildGroundedAnswer(PolicyClause clause, PolicyQuestionRequest request)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"According to [{clause.ClauseId}: {clause.Title}]:");
        sb.AppendLine(clause.Content);

        // If loan underwriter provided live applicant context, apply policy evaluation
        if (request.ApplicantNetSalary.HasValue)
        {
            sb.AppendLine();
            if (clause.ClauseId == "Clause 1.1")
            {
                var sal = request.ApplicantNetSalary.Value;
                bool meetsTier1 = sal >= 25000m;
                bool meetsTier2 = sal >= 20000m;
                sb.AppendLine($"Applicant Evaluation (Net Salary: ₹{sal:N2}):");
                sb.AppendLine($"- Tier-1 Metros (Min ₹25,000): {(meetsTier1 ? "ELIGIBLE" : "INELIGIBLE")}");
                sb.AppendLine($"- Tier-2 Cities (Min ₹20,000): {(meetsTier2 ? "ELIGIBLE" : "INELIGIBLE")}");
            }
            else if (clause.ClauseId == "Clause 3.1")
            {
                var sal = request.ApplicantNetSalary.Value;
                var maxFoir = sal >= 50000m ? 60 : 50;
                var maxMonthlyObligation = sal * (maxFoir / 100m);
                sb.AppendLine($"Applicant Evaluation (Net Salary: ₹{sal:N2}):");
                sb.AppendLine($"- Applicable FOIR Cap: {maxFoir}%");
                sb.AppendLine($"- Maximum Permissible Monthly Debt Obligations (EMIs): ₹{maxMonthlyObligation:N2}");
            }
            else if (clause.ClauseId == "Clause 4.1")
            {
                var sal = request.ApplicantNetSalary.Value;
                var maxQuantum = Math.Min(sal * 10, 1500000m);
                sb.AppendLine($"Applicant Evaluation (Net Salary: ₹{sal:N2}):");
                sb.AppendLine($"- Maximum Permissible Loan Quantum (10x Salary, Max ₹15L): ₹{maxQuantum:N2}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var matches = Regex.Matches(text.ToLowerInvariant(), @"\b[a-z0-9]+\b");
        var tokens = new HashSet<string>();
        foreach (Match m in matches)
        {
            if (m.Value.Length > 1 && !StopWords.Contains(m.Value))
            {
                tokens.Add(m.Value);
            }
        }
        return tokens;
    }
}
