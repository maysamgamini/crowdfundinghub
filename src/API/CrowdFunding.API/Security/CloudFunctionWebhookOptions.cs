namespace CrowdFunding.API.Security;

/// <summary>
/// Binds the <c>CloudFunctionWebhook</c> configuration section — the shared secret used to
/// verify <c>X-Cloud-Signature</c> headers on the inbound media-analysis webhook. See TICKET-032.
/// </summary>
public sealed class CloudFunctionWebhookOptions
{
    public const string SectionName = "CloudFunctionWebhook";

    public string Secret { get; set; } = string.Empty;
}
