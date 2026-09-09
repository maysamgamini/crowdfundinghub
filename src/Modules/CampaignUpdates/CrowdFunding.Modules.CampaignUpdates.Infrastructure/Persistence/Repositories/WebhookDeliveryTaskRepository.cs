using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;

public sealed class WebhookDeliveryTaskRepository : IWebhookDeliveryTaskRepository
{
    private readonly CampaignUpdatesDbContext _dbContext;

    public WebhookDeliveryTaskRepository(CampaignUpdatesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(WebhookDeliveryTask task, CancellationToken cancellationToken)
    {
        _dbContext.WebhookDeliveryTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDeliveryTask>> ClaimDueBatchAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.WebhookDeliveryTasks
            .Where(x => x.Status == WebhookDeliveryStatus.Pending && x.ScheduledAtUtc <= nowUtc)
            .OrderBy(x => x.ScheduledAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(WebhookDeliveryTask task, CancellationToken cancellationToken)
    {
        _dbContext.WebhookDeliveryTasks.Update(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
