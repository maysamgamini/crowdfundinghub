using System.Security.Cryptography;
using System.Text;

namespace CrowdFunding.API.Security;

/// <summary>
/// Verifies an <c>X-Cloud-Signature: sha256=&lt;hex-hmac-sha256&gt;</c> header against the raw
/// webhook request body — the scheme this ticket's own spec calls for (a single HMAC over the
/// whole body, no timestamp/tolerance window, unlike <see cref="StripeWebhookSignatureVerifier"/>'s
/// scheme for TICKET-033). See TICKET-032.
/// </summary>
public static class CloudFunctionSignatureVerifier
{
    private const string Prefix = "sha256=";

    public static bool Verify(string rawBody, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var providedHash = signatureHeader.StartsWith(Prefix, StringComparison.Ordinal)
            ? signatureHeader[Prefix.Length..]
            : signatureHeader;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(providedHash));
    }
}
