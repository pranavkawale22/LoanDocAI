using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoanDocAI.Tests;

public class ExtractionEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static string SyntheticPdfDir =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "synthetic", "pdfs");

    [Fact]
    public async Task Post_extract_with_salary_slip_returns_ok_and_valid_schema()
    {
        var client = factory.CreateClient();
        var slipPath = Path.Combine(SyntheticPdfDir, "slip-01.pdf");
        Assert.True(File.Exists(slipPath));

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(slipPath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "slip-01.pdf");

        var response = await client.PostAsync("/api/extract", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("salary_slip", root.GetProperty("document_type").GetString());
        Assert.Equal("Harish Rao", root.GetProperty("applicant_name").GetString());
        Assert.Equal("Orchid Textiles Ltd", root.GetProperty("employer_name").GetString());
        Assert.Equal("2026-01", root.GetProperty("pay_month").GetString());
        Assert.Equal(119582.00m, root.GetProperty("gross_salary").GetDecimal());
        Assert.Equal(106945.90m, root.GetProperty("net_salary").GetDecimal());
    }

    [Fact]
    public async Task Post_extract_with_bank_statement_returns_ok_and_valid_schema()
    {
        var client = factory.CreateClient();
        var stmtPath = Path.Combine(SyntheticPdfDir, "stmt-01.pdf");
        Assert.True(File.Exists(stmtPath));

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(stmtPath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        content.Add(fileContent, "file", "stmt-01.pdf");

        var response = await client.PostAsync("/api/extract", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("bank_statement", root.GetProperty("document_type").GetString());
        Assert.Equal("Arjun Mishra", root.GetProperty("applicant_name").GetString());
        Assert.Equal("Konkan Savings Bank", root.GetProperty("bank_name").GetString());
        Assert.Equal("2026-03-01", root.GetProperty("statement_from").GetString());
        Assert.Equal("2026-03-31", root.GetProperty("statement_to").GetString());
        Assert.Equal(109850.03m, root.GetProperty("opening_balance").GetDecimal());
        Assert.Equal(23115.09m, root.GetProperty("closing_balance").GetDecimal());
        Assert.Equal(28370.04m, root.GetProperty("total_credits").GetDecimal());
    }

    [Fact]
    public async Task Post_extract_without_file_returns_bad_request()
    {
        var client = factory.CreateClient();
        using var emptyContent = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/extract", emptyContent);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_extract_with_invalid_extension_returns_bad_request()
    {
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        using var fileContent = new StringContent("not a pdf");
        content.Add(fileContent, "file", "document.txt");

        var response = await client.PostAsync("/api/extract", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_reconcile_returns_verdict_and_checks()
    {
        var client = factory.CreateClient();
        var request = new
        {
            salary_slip = new
            {
                applicant_name = "Harish Rao",
                employer_name = "Orchid Textiles Ltd",
                pay_month = "2026-01",
                gross_salary = 120000m,
                net_salary = 100000m
            },
            bank_statement = new
            {
                applicant_name = "Harish Rao",
                bank_name = "Sahyadri Cooperative Bank",
                statement_from = "2026-01-01",
                statement_to = "2026-01-31",
                opening_balance = 25000m,
                closing_balance = 35000m,
                total_credits = 105000m
            }
        };

        var response = await client.PostAsJsonAsync("/api/reconcile", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Approved", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("confidence_score").GetInt32() >= 90);
        Assert.Equal(4, root.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Post_policy_ask_with_valid_question_returns_grounded_answer_and_citations()
    {
        var client = factory.CreateClient();
        var request = new { question = "What is the minimum net take home salary required in Pune Tier 2?" };

        var response = await client.PostAsJsonAsync("/api/policy/ask", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("Answered", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("confidence_score").GetInt32() >= 75);
        Assert.True(root.GetProperty("citations").GetArrayLength() > 0);
        Assert.Contains("Clause 1.1", root.GetProperty("answer").GetString());
    }

    [Fact]
    public async Task Post_policy_ask_with_empty_question_returns_bad_request()
    {
        var client = factory.CreateClient();
        var request = new { question = "   " };

        var response = await client.PostAsJsonAsync("/api/policy/ask", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_policy_ask_with_out_of_scope_query_returns_refusal()
    {
        var client = factory.CreateClient();
        var request = new { question = "Can the borrower pledge Bitcoin crypto assets as collateral for this retail loan?" };

        var response = await client.PostAsJsonAsync("/api/policy/ask", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal("NotFound", root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("confidence_score").GetInt32());
    }
}
