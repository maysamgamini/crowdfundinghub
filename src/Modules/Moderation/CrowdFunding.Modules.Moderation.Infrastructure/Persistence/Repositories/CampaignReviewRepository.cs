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
    public Task AddAsync(CampaignReview campaignReview, CancellationToken cancellationToken)
    {
        // Add (not AddAsync) — EF's AddAsync exists only for value generators that need async DB
        // access (e.g. SQL Server HiLo), which CampaignReview's client-generated Guid key doesn't use.
        _dbContext.CampaignReviews.Add(campaignReview);
        return Task.CompletedTask;
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
