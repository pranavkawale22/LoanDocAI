using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoanDocAI.Tests;

public class HealthProbeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_healthz_liveness_returns_ok_with_self_entry()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        var entries = root.GetProperty("entries");
        Assert.True(entries.TryGetProperty("self", out var selfEntry));
        Assert.Equal("Healthy", selfEntry.GetProperty("status").GetString());
        // Liveness probe must NOT evaluate heavy readiness dependencies
        Assert.False(entries.TryGetProperty("policy_knowledge_base", out _));
        Assert.False(entries.TryGetProperty("extraction_engine", out _));
    }

    [Fact]
    public async Task Get_readyz_readiness_returns_ok_with_policy_and_extraction_engines()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/readyz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        var entries = root.GetProperty("entries");

        Assert.True(entries.TryGetProperty("policy_knowledge_base", out var policyEntry));
        Assert.Equal("Healthy", policyEntry.GetProperty("status").GetString());

        Assert.True(entries.TryGetProperty("extraction_engine", out var extractionEntry));
        Assert.Equal("Healthy", extractionEntry.GetProperty("status").GetString());
    }
}
