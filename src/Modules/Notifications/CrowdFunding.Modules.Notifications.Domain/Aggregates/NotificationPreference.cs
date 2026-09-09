namespace CrowdFunding.Modules.Notifications.Domain.Aggregates;

/// <summary>
/// User-level notification preferences governing commercial and campaign update email dispatches,
/// enforcing GDPR consent and CAN-SPAM opt-out standards (TICKET-056).
/// Transactional notifications (pledge receipts, refund notices) bypass these preferences.
/// </summary>
public sealed class NotificationPreference
{
    public Guid UserId { get; private set; }
    public bool CampaignUpdatesEnabled { get; private set; }
    public bool MarketingAnnouncementsEnabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid userId, bool campaignUpdatesEnabled, bool marketingAnnouncementsEnabled, DateTime updatedAtUtc)
    {
        UserId = userId;
        CampaignUpdatesEnabled = campaignUpdatesEnabled;
        MarketingAnnouncementsEnabled = marketingAnnouncementsEnabled;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static NotificationPreference CreateDefault(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        return new NotificationPreference(userId, campaignUpdatesEnabled: true, marketingAnnouncementsEnabled: false, nowUtc);
    }

    public void Update(bool campaignUpdatesEnabled, bool marketingAnnouncementsEnabled, DateTime nowUtc)
    {
        CampaignUpdatesEnabled = campaignUpdatesEnabled;
        MarketingAnnouncementsEnabled = marketingAnnouncementsEnabled;
        UpdatedAtUtc = nowUtc;
    }
}
