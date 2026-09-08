using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.API.Background;

/// <summary>
/// Processes pending outbox messages and republishes them as application events, using
/// PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> to claim batches safely across multiple running
/// instances. See docs/outbox-architecture.md for the full design and its tradeoffs versus
/// Debezium CDC.
/// </summary>
public sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly IServiceProvider _serviceProvider;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

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

        await ProcessModuleOutboxAsync<CampaignsDbContext>(services, "campaigns_outbox_messages", cancellationToken);
        await ProcessModuleOutboxAsync<ContributionsDbContext>(services, "contributions_outbox_messages", cancellationToken);
        await ProcessModuleOutboxAsync<ModerationDbContext>(services, "moderation_outbox_messages", cancellationToken);
    }

    private async Task ProcessModuleOutboxAsync<TDbContext>(
        IServiceProvider serviceProvider,
        string tableName,
        CancellationToken cancellationToken)
        where TDbContext : DbContext
    {
        var dbContext = serviceProvider.GetRequiredService<TDbContext>();
        var eventPublisher = serviceProvider.GetRequiredService<IEventPublisher>();
        var eventTypeRegistry = serviceProvider.GetRequiredService<EventTypeRegistry>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<OutboxProcessorBackgroundService>();

        var messages = await dbContext.ClaimPendingBatchAsync(tableName, _workerId, BatchSize, LockDuration, cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;

        // Each message is handled independently — one failing or unresolvable message no longer
        // stops the rest of the claimed batch from being attempted (previously: `break;` on the
        // first exception, permanently starving every message behind it — improvement.md §2.1).
        foreach (var message in messages)
        {
            if (!message.TryResolve(eventTypeRegistry, out var applicationEvent, out var failureReason))
            {
                logger.LogWarning(
                    "Routing unresolvable outbox message {MessageId} (EventType={EventType}, Version={Version}) to dead-letter: {Reason}",
                    message.Id, message.EventType, message.Version, failureReason);

                message.MarkDeadLetter(failureReason!);
                dbContext.Set<DeadLetterEvent>().Add(new DeadLetterEvent(
                    message.Id, message.EventType, message.Version, message.Payload, failureReason!, nowUtc));

                continue;
            }

            try
            {
                await eventPublisher.PublishAsync(applicationEvent!, cancellationToken);
                message.MarkProcessed(nowUtc);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish outbox message {MessageId} (EventType={EventType}), attempt {Attempt}/{MaxAttempts}.",
                    message.Id, message.EventType, message.Attempts + 1, MaxAttempts);

                message.MarkFailed(exception.Message, MaxAttempts, RetryDelay, nowUtc);

                if (message.Status == OutboxMessageStatus.DeadLetter)
                {
                    dbContext.Set<DeadLetterEvent>().Add(new DeadLetterEvent(
                        message.Id, message.EventType, message.Version, message.Payload,
                        $"Exceeded {MaxAttempts} attempts. Last error: {exception.Message}", nowUtc));
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
