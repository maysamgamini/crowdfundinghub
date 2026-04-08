using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

public sealed class CampaignRepository : ICampaignRepository
{
    private readonly CampaignsDbContext _dbContext;

    public CampaignRepository(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        await _dbContext.Campaigns.AddAsync(campaign, cancellationToken);
    }

    public async Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
    {
        _dbContext.Campaigns.Update(campaign);
        return Task.CompletedTask;
    }
}
