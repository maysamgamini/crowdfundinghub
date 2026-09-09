using System.Security.Cryptography;
using System.Text;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Security;

/// <summary>
/// Signs outbound webhook payloads with the subscription's own secret — the creator verifies
/// this on their end to confirm a delivery genuinely came from this platform. Mirrors the
/// <c>t=...,v1=...</c> shape used for TICKET-033's inbound Stripe verification, applied here in
/// the opposite direction (signing, not verifying). See TICKET-035.
/// </summary>
public static class WebhookPayloadSigner
{
    public static string Sign(string payload, string secret, DateTimeOffset signedAt)
    {
        var timestamp = signedAt.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={hash}";
    }
}
