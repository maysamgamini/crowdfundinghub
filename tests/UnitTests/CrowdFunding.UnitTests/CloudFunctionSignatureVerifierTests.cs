using System.Security.Cryptography;
using System.Text;
using CrowdFunding.API.Security;

namespace CrowdFunding.UnitTests;

/// <summary>
/// TICKET-032 acceptance criterion #2: <c>X-Cloud-Signature: sha256=...</c> HMAC verification.
/// </summary>
public sealed class CloudFunctionSignatureVerifierTests
{
    private const string Secret = "cfsec_test_secret";

    private static string Sign(string payload, string secret = Secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    [Fact]
    public void Verify_ShouldSucceed_ForAValidSignature()
    {
        var payload = """{"campaignId":"11111111-1111-1111-1111-111111111111"}""";

        var result = CloudFunctionSignatureVerifier.Verify(payload, Sign(payload), Secret);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ShouldFail_WhenTheBodyWasTamperedWithAfterSigning()
    {
        var header = Sign("""{"campaignId":"a"}""");

        var result = CloudFunctionSignatureVerifier.Verify("""{"campaignId":"b"}""", header, Secret);

        Assert.False(result);
    }

    [Fact]
    public void Verify_ShouldFail_WhenSignedWithADifferentSecret()
    {
        var payload = """{"campaignId":"a"}""";

        var result = CloudFunctionSignatureVerifier.Verify(payload, Sign(payload, "wrong-secret"), Secret);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-even-hex")]
    public void Verify_ShouldFail_ForMissingOrMalformedHeaders(string? header)
    {
        var result = CloudFunctionSignatureVerifier.Verify("""{"campaignId":"a"}""", header, Secret);

        Assert.False(result);
    }
}
