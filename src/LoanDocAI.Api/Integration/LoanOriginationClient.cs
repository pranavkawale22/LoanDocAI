using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LoanDocAI.Api.Models;
using Microsoft.Extensions.Options;

namespace LoanDocAI.Api.Integration;

public class LosClientOptions
{
    public const string SectionName = "LosClient";
    public string BaseUrl { get; set; } = "https://los.vedalending.internal/";
    public string ApiKey { get; set; } = "los-internal-dev-key";
}

public record VerificationNotification(
    [property: JsonPropertyName("application_id")] string ApplicationId,
    [property: JsonPropertyName("document_type")] string DocumentType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("extracted_data")] DocumentExtractionResult ExtractedData
);

public interface ILoanOriginationClient
{
    Task<bool> NotifyVerificationCompleteAsync(
        string applicationId,
        DocumentExtractionResult result,
        CancellationToken cancellationToken = default);
}

public sealed class LoanOriginationClient(
    HttpClient httpClient,
    ILogger<LoanOriginationClient> logger) : ILoanOriginationClient
{
    public async Task<bool> NotifyVerificationCompleteAsync(
        string applicationId,
        DocumentExtractionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentNullException.ThrowIfNull(result);

        var payload = new VerificationNotification(
            ApplicationId: applicationId,
            DocumentType: result.DocumentType,
            Status: "extracted",
            ExtractedData: result
        );

        // Security / PII compliance: never log applicant name or salary details
        logger.LogInformation(
            "Sending verification notification for ApplicationId: {ApplicationId}, DocumentType: {DocumentType}",
            applicationId,
            result.DocumentType);

        var response = await httpClient.PostAsJsonAsync(
            $"/api/v1/applications/{applicationId}/verification",
            payload,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Successfully notified LOS for ApplicationId: {ApplicationId}. StatusCode: {StatusCode}",
                applicationId,
                response.StatusCode);
            return true;
        }

        logger.LogWarning(
            "LOS notification failed for ApplicationId: {ApplicationId}. StatusCode: {StatusCode}",
            applicationId,
            response.StatusCode);

        response.EnsureSuccessStatusCode();
        return false;
    }
}
