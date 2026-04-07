using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

public sealed class CampaignReviewReadService : ICampaignReviewReadService
{
    private readonly ModerationDbContext _dbContext;

    public CampaignReviewReadService(ModerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetCampaignReviewByCampaignIdResult?> GetByCampaignIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CampaignReviews
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => new GetCampaignReviewByCampaignIdResult(
                x.CampaignId,
                x.Status.ToString(),
                x.ModeratorId,
                x.Notes,
                x.CreatedAtUtc,
                x.ReviewedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
