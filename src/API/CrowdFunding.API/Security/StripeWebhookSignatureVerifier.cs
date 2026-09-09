using System.Security.Cryptography;
using System.Text;

namespace CrowdFunding.API.Security;

/// <summary>
/// Verifies a <c>Stripe-Signature</c> header against the raw webhook request body, following
/// Stripe's own scheme: <c>t=&lt;unix-seconds&gt;,v1=&lt;hex-hmac-sha256&gt;</c>, where the HMAC is
/// computed over <c>"{timestamp}.{rawBody}"</c> — never the parsed/re-serialized JSON, which can
/// differ byte-for-byte from what was actually signed. See TICKET-033.
/// </summary>
public static class StripeWebhookSignatureVerifier
{
    /// <summary>
    /// Verifies the signature and rejects replays of an old, captured request outside
    /// <paramref name="tolerance"/> — without a tolerance window, a signature computed once
    /// remains valid forever, since HMAC alone proves the payload wasn't tampered with, not that
    /// it's fresh.
    /// </summary>
    public static bool Verify(string rawBody, string? signatureHeader, string secret, DateTimeOffset now, TimeSpan tolerance)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        long? timestamp = null;
        string? signature = null;

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            switch (pair[0])
            {
                case "t" when long.TryParse(pair[1], out var parsedTimestamp):
                    timestamp = parsedTimestamp;
                    break;
                case "v1":
                    signature = pair[1];
                    break;
            }
        }

        if (timestamp is null || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        if ((now - signedAt).Duration() > tolerance)
        {
            return false;
        }

        var signedPayload = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(signature));
    }
}
