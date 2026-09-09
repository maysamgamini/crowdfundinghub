using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

public sealed class RewardTierReservationRepository : IRewardTierReservationRepository
{
    private readonly CampaignsDbContext _dbContext;

    public RewardTierReservationRepository(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(RewardTierReservation reservation, CancellationToken cancellationToken)
    {
        _dbContext.RewardTierReservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<RewardTierReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken)
        => _dbContext.RewardTierReservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

    public async Task<IReadOnlyList<RewardTierReservation>> GetExpiredBatchAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken)
        => await _dbContext.RewardTierReservations
            .Where(x => x.Status == RewardTierReservationStatus.Reserved && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(RewardTierReservation reservation, CancellationToken cancellationToken)
    {
        _dbContext.RewardTierReservations.Update(reservation);
        return Task.CompletedTask;
    }
}
