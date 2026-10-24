using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

/// <summary>The queries behind the "save now, charge at the deadline" funding model.</summary>
public interface IPledgeSettlementStore
{
    /// <summary>Up to <paramref name="batchSize"/> pledges of a campaign in any of the given statuses, ready to be changed.</summary>
    Task<IReadOnlyList<Contribution>> GetBatchAsync(
        Guid campaignId,
        IReadOnlyCollection<ContributionStatus> statuses,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims pledges that are due for a charge attempt, many workers at once without collisions
    /// (<c>FOR UPDATE SKIP LOCKED</c>). Two kinds of rows qualify: <c>ToCharge</c> pledges whose time has come, which
    /// start the NEXT numbered attempt, and <c>Charging</c> pledges whose lease ran out because their worker died,
    /// which resume the SAME attempt. All times are the application's clock.
    /// </summary>
    Task<IReadOnlyList<Contribution>> ClaimDueForChargeAsync(
        DateTime nowUtc,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Returns the attempt record, creating it (in flight) if this is the first time the attempt is seen.</summary>
    Task<ChargeAttempt> GetOrCreateAttemptAsync(
        Guid contributionId,
        int attemptNumber,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
