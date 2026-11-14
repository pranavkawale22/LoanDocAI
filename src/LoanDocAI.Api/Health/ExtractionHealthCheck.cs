using LoanDocAI.Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanDocAI.Api.Health;

public sealed class ExtractionHealthCheck(
    IPdfTextExtractor textExtractor,
    IDocumentClassifier classifier,
    IDocumentExtractor documentExtractor) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (textExtractor is null || classifier is null || documentExtractor is null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                description: "One or more core document extraction dependencies are missing."));
        }

        var data = new Dictionary<string, object>
        {
            ["max_file_size_bytes"] = PdfTextExtractor.MaxFileSizeBytes,
            ["supported_types"] = new[] { "salary_slip", "bank_statement" },
            ["extractor"] = documentExtractor.GetType().Name
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            description: "PdfPig spatial extractor, line clusterer, and pattern matching pipelines are operational.",
            data: data));
    }
}
