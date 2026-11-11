using System.Net;
using System.Text;
using LoanDocAI.Api.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoanDocAI.Tests;

public class WebhookEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Post_webhook_without_signature_header_returns_unauthorized()
    {
        var client = factory.CreateClient();
        var content = new StringContent("{\"event\":\"doc_ready\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/document-uploaded", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_webhook_with_forged_signature_returns_forbidden()
    {
        var client = factory.CreateClient();
        var content = new StringContent("{\"event\":\"doc_ready\"}", Encoding.UTF8, "application/json");
        content.Headers.Add("X-Signature", "forged-signature-value-1234567890abcdef");

        var response = await client.PostAsync("/api/webhooks/document-uploaded", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_webhook_with_valid_hmac_signature_returns_accepted()
    {
        var client = factory.CreateClient();
        var validator = factory.Services.GetRequiredService<IHmacSignatureValidator>();

        var rawJson = "{\"event\":\"doc_ready\",\"application_id\":\"APP-8831\"}";
        var payloadBytes = Encoding.UTF8.GetBytes(rawJson);
        var signature = validator.ComputeSignature(payloadBytes);

        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Signature", signature);

        var response = await client.PostAsync("/api/webhooks/document-uploaded", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
