using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for Campaign Review.
/// </summary>
public sealed class CampaignReviewRepository : ICampaignReviewRepository
{
    private readonly ModerationDbContext _dbContext;

    public CampaignReviewRepository(ModerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task AddAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        await _dbContext.CampaignReviews.AddAsync(campaignReview, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CampaignReview?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return _dbContext.CampaignReviews
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        _dbContext.CampaignReviews.Update(campaignReview);
        return Task.CompletedTask;
    }
}
