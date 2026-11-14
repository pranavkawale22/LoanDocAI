using LoanDocAI.Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanDocAI.Api.Health;

public sealed class PolicyHealthCheck(IPolicyQaService policyService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!policyService.IsLoaded)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                description: "Credit underwriting policy data is missing or empty."));
        }

        var data = new Dictionary<string, object>
        {
            ["policy_name"] = policyService.PolicyName,
            ["clause_count"] = policyService.ClauseCount
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            description: $"Grounded policy '{policyService.PolicyName}' active ({policyService.ClauseCount} clauses loaded).",
            data: data));
    }
}
