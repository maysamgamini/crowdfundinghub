using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;

public sealed class WebhookSubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly CampaignUpdatesDbContext _dbContext;

    public WebhookSubscriptionRepository(CampaignUpdatesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        _dbContext.WebhookSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<WebhookSubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken)
        => _dbContext.WebhookSubscriptions.SingleOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.WebhookSubscriptions
            .Where(x => x.CampaignId == campaignId && x.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken)
        => await _dbContext.WebhookSubscriptions
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        _dbContext.WebhookSubscriptions.Update(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
