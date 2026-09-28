namespace CrowdFunding.Modules.CampaignUpdates.Contracts.Webhooks;

/// <summary>
/// How a subscriber verifies that a webhook came from this platform and was not replayed.
/// </summary>
/// <remarks>
/// The <see cref="HeaderName"/> header has the form <c>t={unixSeconds},v1={hex}</c>, where
/// <c>hex</c> is the lowercase HMAC-SHA256, keyed with the subscription's secret, of
/// <c>{unixSeconds}.{raw request body}</c>. Receivers recompute it over the exact body bytes,
/// compare in constant time, and reject timestamps outside their tolerance window.
/// </remarks>
public static class WebhookSignature
{
    /// <summary>The request header carrying the signature.</summary>
    public const string HeaderName = "X-CrowdFunding-Signature";
}
