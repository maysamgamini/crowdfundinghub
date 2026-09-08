namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

/// <summary>
/// Append-only record of a contribution that has been applied to a campaign's raised amount.
/// The unique constraint on <see cref="ContributionId"/> is the idempotency guard: at-least-once
/// delivery of the confirmation event can safely retry recording the same contribution without
/// double-crediting the campaign balance.
/// </summary>
public sealed class ContributionLedgerEntry
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ContributionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime RecordedAtUtc { get; private set; }

    private ContributionLedgerEntry()
    {
    }

    public ContributionLedgerEntry(Guid campaignId, Guid contributionId, decimal amount, string currency, DateTime recordedAtUtc)
    {
        Id = Guid.NewGuid();
        CampaignId = campaignId;
        ContributionId = contributionId;
        Amount = amount;
        Currency = currency;
        RecordedAtUtc = recordedAtUtc;
    }
}
