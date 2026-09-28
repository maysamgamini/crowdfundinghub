using CrowdFunding.BuildingBlocks.Domain.Exceptions;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Enums;

namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

/// <summary>
/// One immutable line of a campaign's append-only funding ledger. A campaign's raised amount is
/// the sum of its entries; entries are only ever inserted, never updated or deleted.
/// </summary>
/// <remarks>
/// <para>
/// Created only through <see cref="Campaign.RecordPledge"/> and <see cref="Campaign.RecordRefund"/>,
/// which enforce the campaign's rules. The database enforces the rest, independent of any code
/// path: a unique key on (<see cref="ContributionId"/>, <see cref="Kind"/>) makes a redelivered
/// pledge or refund a no-op, a CHECK constraint ties the sign of <see cref="Amount"/> to the kind,
/// and a trigger rejects UPDATE and DELETE.
/// </para>
/// <para>
/// A refund is a compensating entry with a negative amount, never an edit of the pledge it
/// reverses, so the history of what happened is preserved and auditable.
/// </para>
/// </remarks>
public sealed class CampaignLedgerEntry
{
    private CampaignLedgerEntry()
    {
    }

    private CampaignLedgerEntry(Guid campaignId, Guid contributionId, CampaignLedgerEntryKind kind, decimal signedAmount, string currency, DateTime recordedAtUtc)
    {
        Id = Guid.NewGuid();
        CampaignId = campaignId;
        ContributionId = contributionId;
        Kind = kind;
        Amount = signedAmount;
        Currency = currency;
        RecordedAtUtc = recordedAtUtc;
    }

    /// <summary>Gets the entry id.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the campaign this entry belongs to.</summary>
    public Guid CampaignId { get; private set; }

    /// <summary>
    /// Gets the contribution this entry credits or reverses; <see langword="null"/> only for an
    /// <see cref="CampaignLedgerEntryKind.OpeningBalance"/>. Together with <see cref="Kind"/> it is
    /// the idempotency key.
    /// </summary>
    public Guid? ContributionId { get; private set; }

    /// <summary>Gets what the entry records.</summary>
    public CampaignLedgerEntryKind Kind { get; private set; }

    /// <summary>Gets the signed amount: positive for a pledge, negative for a refund.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the ISO 4217 currency, always the campaign's currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the UTC time the entry was recorded.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    internal static CampaignLedgerEntry Pledge(Guid campaignId, Guid contributionId, Money amount, DateTime recordedAtUtc)
        => new(campaignId, RequireContribution(contributionId), CampaignLedgerEntryKind.Pledge, amount.Amount, amount.Currency, recordedAtUtc);

    internal static CampaignLedgerEntry Refund(Guid campaignId, Guid contributionId, Money amount, DateTime recordedAtUtc)
        => new(campaignId, RequireContribution(contributionId), CampaignLedgerEntryKind.Refund, -amount.Amount, amount.Currency, recordedAtUtc);

    private static Guid RequireContribution(Guid contributionId)
        => contributionId != Guid.Empty ? contributionId : throw new BusinessRuleViolationException("ContributionId is required.");
}
