using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

public interface ICampaignRepository
{
    Task AddAsync(Campaign campaign, CancellationToken cancellationToken);
    Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken);
}
