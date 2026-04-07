using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

public sealed class CampaignFundingService : ICampaignFundingService
{
    private readonly CampaignsDbContext _campaignsDbContext;

    public CampaignFundingService(CampaignsDbContext campaignsDbContext)
    {
        _campaignsDbContext = campaignsDbContext;
    }

    public async Task AddContributionAsync(
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignsDbContext.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{campaignId}' was not found.");
        }

        campaign.AddContribution(new Money(amount, currency));

        await _campaignsDbContext.SaveChangesAsync(cancellationToken);
    }
}
