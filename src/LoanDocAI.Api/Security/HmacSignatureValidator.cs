using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LoanDocAI.Api.Security;

public class WebhookOptions
{
    public const string SectionName = "Webhook";
    public string Secret { get; set; } = "default-dev-secret-key-32-chars-min!";
}

public interface IHmacSignatureValidator
{
    bool Validate(ReadOnlySpan<byte> bodyBytes, string? signatureHeader);
    string ComputeSignature(ReadOnlySpan<byte> bodyBytes);
}

public sealed class HmacSignatureValidator(IOptions<WebhookOptions> options) : IHmacSignatureValidator
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Secret);

    public string ComputeSignature(ReadOnlySpan<byte> bodyBytes)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bodyBytes.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    public bool Validate(ReadOnlySpan<byte> bodyBytes, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        var computedHex = ComputeSignature(bodyBytes);
        var cleanHeader = signatureHeader.Trim().ToLowerInvariant();

        // Strip optional prefix like "sha256=" if partner sends it
        if (cleanHeader.StartsWith("sha256="))
        {
            cleanHeader = cleanHeader["sha256=".Length..];
        }

        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        var headerBytes = Encoding.UTF8.GetBytes(cleanHeader);

        if (computedBytes.Length != headerBytes.Length)
            return false;

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedBytes, headerBytes);
    }
}
