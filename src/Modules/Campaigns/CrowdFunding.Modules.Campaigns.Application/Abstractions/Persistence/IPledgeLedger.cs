namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

/// <summary>
/// Append-only, idempotent record of pledges applied to a campaign's raised amount.
/// </summary>
public interface IPledgeLedger
{
    /// <summary>
    /// Attempts to record a pledge against a campaign. Returns <c>false</c> without
    /// throwing when <paramref name="pledgeId"/> has already been recorded (redelivery of
    /// the same confirmation event), so callers can treat that case as an idempotent no-op
    /// rather than re-applying the balance change.
    /// </summary>
    Task<bool> TryRecordAsync(
        Guid campaignId,
        Guid pledgeId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the compensating entry for a refunded or refused pledge (stored as a negative amount).
    /// Returns false when the refund was already recorded.
    /// </summary>
    Task<bool> TryRecordRefundAsync(
        Guid campaignId,
        Guid pledgeId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);

    /// <summary>True when a pledge entry for this pledge exists.</summary>
    Task<bool> HasPledgeAsync(Guid pledgeId, CancellationToken cancellationToken);

    /// <summary>True when a refund entry for this pledge exists.</summary>
    Task<bool> HasRefundAsync(Guid pledgeId, CancellationToken cancellationToken);

    /// <summary>The exact total of a campaign: every pledge minus every refund. This, not a stored counter, is what a funding decision can trust.</summary>
    Task<decimal> SumAsync(Guid campaignId, CancellationToken cancellationToken);
}
