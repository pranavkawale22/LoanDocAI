using LoanDocAI.Api;
using LoanDocAI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOptions<AiOptions>()
    .BindConfiguration(AiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register Document Extraction Services
builder.Services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddSingleton<IDocumentClassifier, DocumentClassifier>();
builder.Services.AddSingleton<IDocumentExtractor, PatternDocumentExtractor>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

app.Run();

public partial class Program;
