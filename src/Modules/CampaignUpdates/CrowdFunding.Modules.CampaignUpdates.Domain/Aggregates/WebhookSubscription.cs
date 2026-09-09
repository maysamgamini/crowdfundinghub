using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

/// <summary>
/// A creator-registered outbound webhook target for one campaign's pledge activity (their CRM,
/// Zapier, an accounting system). Owns its own delivery-health state so a persistently failing
/// target degrades itself out of the dispatch loop instead of being retried forever. See
/// TICKET-035.
/// </summary>
public sealed class WebhookSubscription : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    public string SecretKey { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int ConsecutiveFailureCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private WebhookSubscription()
    {
    }

    private WebhookSubscription(Guid id, Guid campaignId, string targetUrl, string secretKey, DateTime createdAtUtc)
    {
        Id = id;
        CampaignId = campaignId;
        TargetUrl = targetUrl;
        SecretKey = secretKey;
        IsActive = true;
        ConsecutiveFailureCount = 0;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Registers a new webhook subscription. Callers must validate <paramref name="targetUrl"/>
    /// against SSRF/private-network rules (<c>UrlSecurityValidator</c>) before calling this — the
    /// aggregate only enforces the shape/presence of its fields, not network-reachability policy,
    /// since that policy is infrastructure-layer I/O (DNS resolution), not a domain invariant.
    /// </summary>
    public static WebhookSubscription Create(Guid campaignId, string targetUrl, string secretKey, DateTime createdAtUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            throw new ArgumentException("Target URL is required.", nameof(targetUrl));
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentException("Secret key is required.", nameof(secretKey));
        }

        return new WebhookSubscription(Guid.NewGuid(), campaignId, targetUrl.Trim(), secretKey, createdAtUtc);
    }

    /// <summary>Resets the consecutive-failure counter after a successful delivery — only
    /// unbroken runs of failure count toward disabling the subscription.</summary>
    public void RecordDeliverySuccess()
    {
        ConsecutiveFailureCount = 0;
    }

    /// <summary>
    /// Records a failed delivery attempt, disabling the subscription once
    /// <paramref name="maxConsecutiveFailures"/> is reached — the "Degraded/Disabled" state this
    /// ticket's Dead-Letter Recovery criterion calls for. A disabled subscription is simply
    /// excluded from future dispatch; re-enabling it is a deliberate, separate creator action
    /// (not implemented here — out of this ticket's tested scope).
    /// </summary>
    public void RecordDeliveryFailure(int maxConsecutiveFailures)
    {
        ConsecutiveFailureCount++;

        if (ConsecutiveFailureCount >= maxConsecutiveFailures)
        {
            IsActive = false;
        }
    }
}
