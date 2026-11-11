using System.Text;
using LoanDocAI.Api.Security;
using Microsoft.Extensions.Options;

namespace LoanDocAI.Tests;

public class HmacValidatorTests
{
    private readonly HmacSignatureValidator _validator;

    public HmacValidatorTests()
    {
        var options = Options.Create(new WebhookOptions
        {
            Secret = "super-secret-enterprise-key-12345"
        });
        _validator = new HmacSignatureValidator(options);
    }

    [Fact]
    public void Valid_signature_passes_validation()
    {
        var payload = Encoding.UTF8.GetBytes("{\"applicant_id\":\"APP-123\",\"status\":\"uploaded\"}");
        var signature = _validator.ComputeSignature(payload);

        var isValid = _validator.Validate(payload, signature);

        Assert.True(isValid);
    }

    [Fact]
    public void Signature_with_sha256_prefix_passes_validation()
    {
        var payload = Encoding.UTF8.GetBytes("{\"applicant_id\":\"APP-123\",\"status\":\"uploaded\"}");
        var signature = "sha256=" + _validator.ComputeSignature(payload);

        var isValid = _validator.Validate(payload, signature);

        Assert.True(isValid);
    }

    [Fact]
    public void Tampered_payload_fails_validation()
    {
        var originalPayload = Encoding.UTF8.GetBytes("{\"applicant_id\":\"APP-123\",\"amount\":50000}");
        var signature = _validator.ComputeSignature(originalPayload);

        // Attacker altered amount to 500000
        var tamperedPayload = Encoding.UTF8.GetBytes("{\"applicant_id\":\"APP-123\",\"amount\":500000}");

        var isValid = _validator.Validate(tamperedPayload, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void Empty_or_null_signature_fails_validation()
    {
        var payload = Encoding.UTF8.GetBytes("test payload");

        Assert.False(_validator.Validate(payload, null));
        Assert.False(_validator.Validate(payload, ""));
        Assert.False(_validator.Validate(payload, "   "));
    }

    [Fact]
    public void Forged_signature_fails_validation()
    {
        var payload = Encoding.UTF8.GetBytes("test payload");
        var forged = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

        Assert.False(_validator.Validate(payload, forged));
    }
}
