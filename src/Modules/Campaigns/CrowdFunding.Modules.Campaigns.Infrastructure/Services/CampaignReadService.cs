using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

public sealed class CampaignReadService : ICampaignReadService
{
    private readonly CampaignsDbContext _dbContext;

    public CampaignReadService(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetCampaignByIdResult?> GetByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .AsNoTracking()
            .Where(x => x.Id == campaignId)
            .Select(x => new GetCampaignByIdResult(
                x.Id,
                x.OwnerId,
                x.Title,
                x.Story,
                x.Category,
                x.GoalAmount.Amount,
                x.GoalAmount.Currency,
                x.RaisedAmount.Amount,
                x.RaisedAmount.Currency,
                x.DeadlineUtc,
                x.Status.ToString(),
                x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}