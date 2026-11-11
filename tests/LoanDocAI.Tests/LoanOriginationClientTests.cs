using System.Net;
using LoanDocAI.Api.Integration;
using LoanDocAI.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;

namespace LoanDocAI.Tests;

public class LoanOriginationClientTests
{
    private class FlakyHttpHandler(int failuresBeforeSuccess, HttpStatusCode failureStatusCode) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount <= failuresBeforeSuccess)
            {
                return Task.FromResult(new HttpResponseMessage(failureStatusCode));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task Retries_transient_http_503_and_succeeds()
    {
        // Fail twice with 503, succeed on 3rd attempt
        var handler = new FlakyHttpHandler(failuresBeforeSuccess: 2, failureStatusCode: HttpStatusCode.ServiceUnavailable);

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new LosClientOptions
        {
            BaseUrl = "https://mock.los.local/",
            ApiKey = "test-key"
        }));

        services.AddHttpClient<ILoanOriginationClient, LoanOriginationClient>(client =>
        {
            client.BaseAddress = new Uri("https://mock.los.local/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => handler)
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(5); // fast for tests
            options.Retry.BackoffType = DelayBackoffType.Constant;
            options.Retry.UseJitter = false;
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILoanOriginationClient>();

        var dummySlip = new SalarySlipExtraction
        {
            ApplicantName = "Test Applicant",
            EmployerName = "Acme Corp",
            GrossSalary = 100000m,
            NetSalary = 80000m,
            PayMonth = "2026-01"
        };

        var result = await client.NotifyVerificationCompleteAsync("APP-9901", dummySlip);

        Assert.True(result);
        Assert.Equal(3, handler.CallCount); // 1 initial try + 2 retries = 3 calls
    }

    [Fact]
    public async Task Fails_when_downstream_service_is_permanently_unavailable()
    {
        // Fails permanently with 503
        var handler = new FlakyHttpHandler(failuresBeforeSuccess: 10, failureStatusCode: HttpStatusCode.ServiceUnavailable);

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new LosClientOptions
        {
            BaseUrl = "https://mock.los.local/",
            ApiKey = "test-key"
        }));

        services.AddHttpClient<ILoanOriginationClient, LoanOriginationClient>(client =>
        {
            client.BaseAddress = new Uri("https://mock.los.local/");
        })
        .ConfigurePrimaryHttpMessageHandler(() => handler)
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(5);
            options.Retry.BackoffType = DelayBackoffType.Constant;
            options.Retry.UseJitter = false;
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILoanOriginationClient>();

        var dummySlip = new SalarySlipExtraction
        {
            ApplicantName = "Test Applicant",
            EmployerName = "Acme Corp",
            GrossSalary = 100000m,
            NetSalary = 80000m,
            PayMonth = "2026-01"
        };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.NotifyVerificationCompleteAsync("APP-9902", dummySlip));

        // 1 initial attempt + 2 retries = 3 calls total before giving up
        Assert.Equal(3, handler.CallCount);
    }
}
