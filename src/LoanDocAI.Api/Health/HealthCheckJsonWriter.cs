using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoanDocAI.Api.Health;

public static class HealthCheckJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            total_duration_ms = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description,
                    duration_ms = kvp.Value.Duration.TotalMilliseconds,
                    data = kvp.Value.Data
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
