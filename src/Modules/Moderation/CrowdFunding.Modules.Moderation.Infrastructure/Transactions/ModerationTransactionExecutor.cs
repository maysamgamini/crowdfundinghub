using CrowdFunding.BuildingBlocks.Application.Outbox;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.Modules.Moderation.Domain.Events;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Transactions;

/// <summary>
/// Executes the surrounding module write operation inside a transaction boundary.
/// </summary>
public sealed class ModerationTransactionExecutor : IModerationTransactionExecutor
{
    private readonly ModerationDbContext _dbContext;

    private readonly IOutboxSignal<ModerationDbContext>? _outboxSignal;

    private readonly InboxRecorder<ModerationDbContext>? _inboxRecorder;

    public ModerationTransactionExecutor(ModerationDbContext dbContext, IOutboxSignal<ModerationDbContext>? outboxSignal = null,
        InboxRecorder<ModerationDbContext>? inboxRecorder = null)
    {
        _dbContext = dbContext;
        _outboxSignal = outboxSignal;
        _inboxRecorder = inboxRecorder;
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        => ExecuteAsync<object?>(async ct => { await action(ct); return null; }, cancellationToken);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await action(cancellationToken);

            if (ownsTransaction)
            {
                var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
                var outboxMessages = domainEvents.Select(MapApplicationEvent).ToArray();

                if (outboxMessages.Length > 0)
                {
                    await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                // A handler that asked for it gets its inbox row in this same transaction.
                if (_inboxRecorder is not null)
                {
                    await _inboxRecorder.RecordIfRequestedAsync(cancellationToken);
                }

                DomainEventAccessor.ClearDomainEvents(_dbContext);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);

                    // The rows are durable now, so wake this module's outbox worker instead of
                    // leaving them to wait for its next timer tick. Waking before the commit would
                    // let the worker look, find nothing, and go back to sleep.
                    if (outboxMessages.Length > 0)
                    {
                        _outboxSignal?.Notify();
                    }
                }
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static OutboxMessage MapApplicationEvent(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            CampaignReviewApprovedDomainEvent @event => OutboxMessage.Create(
                new CampaignReviewApprovedApplicationEvent(@event.CampaignId, @event.ModeratorId, @event.Notes),
                DateTime.UtcNow),
            CampaignReviewRejectedDomainEvent @event => OutboxMessage.Create(
                new CampaignReviewRejectedApplicationEvent(@event.CampaignId, @event.ModeratorId, @event.Notes),
                DateTime.UtcNow),
            // Fail loud instead of silently discarding (improvement.md §2.3): an unmapped domain
            // event throws immediately rather than vanishing with no log and no side effect.
            _ => throw new InvalidOperationException(
                $"No outbox mapping is registered for domain event '{domainEvent.GetType().Name}'.")
        };
    }
}
