using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

public interface IRewardTierReservationRepository
{
    Task AddAsync(RewardTierReservation reservation, CancellationToken cancellationToken);
    Task<RewardTierReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>Every <c>Reserved</c> reservation whose <c>ExpiresAtUtc</c> has passed, up to
    /// <paramref name="batchSize"/> — the scavenger's poll loop.</summary>
    Task<IReadOnlyList<RewardTierReservation>> GetExpiredBatchAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken);

    Task UpdateAsync(RewardTierReservation reservation, CancellationToken cancellationToken);
}
