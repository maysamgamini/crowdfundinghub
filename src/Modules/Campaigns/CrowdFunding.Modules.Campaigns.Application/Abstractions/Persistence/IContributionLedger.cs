namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

/// <summary>
/// Append-only, idempotent record of contributions applied to a campaign's raised amount.
/// </summary>
public interface IContributionLedger
{
    /// <summary>
    /// Attempts to record a contribution against a campaign. Returns <c>false</c> without
    /// throwing when <paramref name="contributionId"/> has already been recorded (redelivery of
    /// the same confirmation event), so callers can treat that case as an idempotent no-op
    /// rather than re-applying the balance change.
    /// </summary>
    Task<bool> TryRecordAsync(
        Guid campaignId,
        Guid contributionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);
}
