using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

/// <summary>Persistence for refund jobs and the record of each answered refund attempt.</summary>
public interface IRefundStore
{
    Task<bool> JobExistsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Stages a new job; the database refuses a second one for the same campaign.</summary>
    Task AddJobAsync(RefundJob job, CancellationToken cancellationToken);

    /// <summary>Jobs that still have pledges to refund, oldest first.</summary>
    Task<IReadOnlyList<RefundJob>> GetOpenJobsAsync(int limit, CancellationToken cancellationToken);

    Task UpdateJobAsync(RefundJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Paid pledges of the campaign that may be refunded now: not refused too many times and not waiting out a retry delay.
    /// </summary>
    Task<IReadOnlyList<Pledge>> GetRefundablePledgesAsync(Guid campaignId, DateTime nowUtc, int maxAttempts, int limit, CancellationToken cancellationToken);

    /// <summary>How many paid pledges of the campaign are still to be refunded, and how many of those used up their attempts.</summary>
    Task<RefundProgress> GetProgressAsync(Guid campaignId, int maxAttempts, CancellationToken cancellationToken);

    /// <summary>Stages the record of one answered attempt.</summary>
    Task AddAttemptAsync(RefundAttempt attempt, CancellationToken cancellationToken);
}

/// <summary>Where a campaign's refunds stand: how many paid pledges remain, and how many of them gave up.</summary>
public sealed record RefundProgress(int Remaining, int Exhausted);
