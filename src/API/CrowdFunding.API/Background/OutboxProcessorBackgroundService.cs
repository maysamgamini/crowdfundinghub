using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.API.Background;

public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceProvider _serviceProvider;

    public OutboxProcessorBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxBatchAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessOutboxBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        await ProcessModuleOutboxAsync<CampaignsDbContext>(services, cancellationToken);
        await ProcessModuleOutboxAsync<ContributionsDbContext>(services, cancellationToken);
        await ProcessModuleOutboxAsync<ModerationDbContext>(services, cancellationToken);
    }

    private static async Task ProcessModuleOutboxAsync<TDbContext>(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        where TDbContext : DbContext
    {
        var dbContext = serviceProvider.GetRequiredService<TDbContext>();
        var eventPublisher = serviceProvider.GetRequiredService<IEventPublisher>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(x => x.ProcessedOnUtc == null)
            .OrderBy(x => x.OccurredOnUtc)
            .ThenBy(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var applicationEvent = message.Deserialize();
                await eventPublisher.PublishAsync(applicationEvent, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message);
                break;
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
