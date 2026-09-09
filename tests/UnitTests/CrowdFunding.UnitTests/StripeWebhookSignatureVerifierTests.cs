using System.Security.Cryptography;
using System.Text;
using CrowdFunding.API.Security;

namespace CrowdFunding.UnitTests;

/// <summary>
/// TICKET-033 acceptance criterion #2: HMAC-SHA256 signature verification with a timestamp
/// tolerance window, matching Stripe's own <c>t=...,v1=...</c> scheme.
/// </summary>
public sealed class StripeWebhookSignatureVerifierTests
{
    private const string Secret = "whsec_test_secret";

    private static string SignHeader(string payload, DateTimeOffset signedAt, string secret = Secret)
    {
        var timestamp = signedAt.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={hash}";
    }

    [Fact]
    public void Verify_ShouldSucceed_ForAFreshValidSignature()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = """{"id":"evt_1","type":"payment_intent.succeeded"}""";
        var header = SignHeader(payload, now);

        var result = StripeWebhookSignatureVerifier.Verify(payload, header, Secret, now, TimeSpan.FromSeconds(300));

        Assert.True(result);
    }

    [Fact]
    public void Verify_ShouldFail_WhenTheBodyWasTamperedWithAfterSigning()
    {
        var now = DateTimeOffset.UtcNow;
        var header = SignHeader("""{"id":"evt_1"}""", now);

        var result = StripeWebhookSignatureVerifier.Verify("""{"id":"evt_2"}""", header, Secret, now, TimeSpan.FromSeconds(300));

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldFail_WhenSignedWithADifferentSecret()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = """{"id":"evt_1"}""";
        var header = SignHeader(payload, now, secret: "whsec_wrong_secret");

        var result = StripeWebhookSignatureVerifier.Verify(payload, header, Secret, now, TimeSpan.FromSeconds(300));

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldFail_WhenTheTimestampIsOutsideTheToleranceWindow()
    {
        var signedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var payload = """{"id":"evt_1"}""";
        var header = SignHeader(payload, signedAt);

        var result = StripeWebhookSignatureVerifier.Verify(payload, header, Secret, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(300));

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage-not-a-signature")]
    [InlineData("t=not-a-number,v1=abc")]
    public void Verify_ShouldFail_ForMissingOrMalformedHeaders(string? header)
    {
        var result = StripeWebhookSignatureVerifier.Verify("""{"id":"evt_1"}""", header, Secret, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(300));

        Assert.False(result);
    }
}
