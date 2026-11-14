using LoanDocAI.Api;
using LoanDocAI.Api.Health;
using LoanDocAI.Api.Integration;
using LoanDocAI.Api.Models;
using LoanDocAI.Api.Security;
using LoanDocAI.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOptions<AiOptions>()
    .BindConfiguration(AiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Enterprise Health Checks (Liveness and Readiness Probes)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API process is alive and responsive."), tags: ["live"])
    .AddCheck<PolicyHealthCheck>("policy_knowledge_base", tags: ["ready"])
    .AddCheck<ExtractionHealthCheck>("extraction_engine", tags: ["ready"]);

// Security & Webhook configuration
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.AddSingleton<IHmacSignatureValidator, HmacSignatureValidator>();

// Document Extraction & Reconciliation Services
builder.Services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddSingleton<IDocumentClassifier, DocumentClassifier>();
builder.Services.AddSingleton<IDocumentExtractor, PatternDocumentExtractor>();
builder.Services.AddSingleton<IIncomeReconciliationService, IncomeReconciliationService>();
builder.Services.AddSingleton<IPolicyQaService, PolicyQaService>();

// Downstream Integration with Polly v8 Resilience Pipeline
builder.Services.Configure<LosClientOptions>(builder.Configuration.GetSection(LosClientOptions.SectionName));
builder.Services.AddHttpClient<ILoanOriginationClient, LoanOriginationClient>((sp, client) =>
{
    var losOptions = sp.GetRequiredService<IOptions<LosClientOptions>>().Value;
    client.BaseAddress = new Uri(losOptions.BaseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", losOptions.ApiKey);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Serve the embedded Operator Dashboard from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enterprise Health Probes for Orchestrators (Kubernetes / ECS / Docker)
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckJsonWriter.WriteResponse
});

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckJsonWriter.WriteResponse
});

// Backward-compatible lightweight health check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Public Document Extraction Endpoint
app.MapPost("/api/extract", async (HttpRequest request, IDocumentExtractor extractor, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Request must be multipart/form-data." });
    }

    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync(ct);
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Malformed or empty multipart form data." });
    }

    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "No file provided or file is empty." });
    }

    if (file.Length > PdfTextExtractor.MaxFileSizeBytes)
    {
        return Results.BadRequest(new { error = $"File size exceeds 5 MB limit (Actual: {file.Length} bytes)." });
    }

    if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && file.ContentType != "application/pdf")
    {
        return Results.BadRequest(new { error = "Only text-based PDF files are supported." });
    }

    using var stream = file.OpenReadStream();
    try
    {
        var result = await extractor.ExtractAsync(stream, ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "Extraction Failed");
    }
}).DisableAntiforgery();

// Cross-Document Income Reconciliation Endpoint
app.MapPost("/api/reconcile", (ReconciliationRequest request, IIncomeReconciliationService service) =>
{
    if (request.SalarySlip is null || request.BankStatement is null)
    {
        return Results.BadRequest(new { error = "Both salary_slip and bank_statement are required." });
    }

    var result = service.Reconcile(request.SalarySlip, request.BankStatement);
    return Results.Ok(result);
});

// Grounded Policy Q&A Endpoint with Citations
app.MapPost("/api/policy/ask", async (PolicyQuestionRequest request, IPolicyQaService policyService, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question is required." });
    }

    var result = await policyService.AnswerQuestionAsync(request, ct);
    return Results.Ok(result);
});

// HMAC-Secured Partner Webhook Endpoint
app.MapPost("/api/webhooks/document-uploaded", async (
    HttpRequest request,
    IHmacSignatureValidator signatureValidator,
    CancellationToken ct) =>
{
    if (!request.Headers.TryGetValue("X-Signature", out var signature))
    {
        return Results.Unauthorized();
    }

    using var memoryStream = new MemoryStream();
    await request.Body.CopyToAsync(memoryStream, ct);
    var bodyBytes = memoryStream.ToArray();

    if (!signatureValidator.Validate(bodyBytes, signature))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Accepted(value: new { status = "webhook_verified" });
});

app.Run();

public partial class Program;
