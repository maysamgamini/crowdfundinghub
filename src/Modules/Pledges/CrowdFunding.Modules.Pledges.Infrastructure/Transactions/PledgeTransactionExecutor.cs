using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeActivated;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeReleased;
using CrowdFunding.BuildingBlocks.Application.Outbox;
using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentConfirmed;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentFailed;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeRefunded;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Events;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Transactions;

/// <summary>
/// Executes the surrounding module write operation inside a transaction boundary.
/// </summary>
public sealed class PledgeTransactionExecutor : IPledgeTransactionExecutor
{
    private readonly PledgesDbContext _dbContext;

    private readonly IOutboxSignal<PledgesDbContext>? _outboxSignal;

    private readonly InboxRecorder<PledgesDbContext>? _inboxRecorder;

    public PledgeTransactionExecutor(PledgesDbContext dbContext, IOutboxSignal<PledgesDbContext>? outboxSignal = null,
        InboxRecorder<PledgesDbContext>? inboxRecorder = null)
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
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            // Translated to an infrastructure-agnostic exception so application-layer handlers
            // can catch and retry without taking a dependency on Entity Framework Core (mirrors
            // CampaignTransactionExecutor).
            throw new ConcurrencyConflictException(
                "The aggregate was modified by another transaction. Retry with a fresh read.", ex);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            // The failed insert is still tracked as Added; drop it so the caller's re-read of the
            // winning pledge is not confused by a half-saved twin.
            _dbContext.ChangeTracker.Clear();
            throw new IdempotencyKeyAlreadyUsedException(ex);
        }
        catch (DbUpdateException ex) when (IsOneLivePledgeViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();
            throw new AlreadyBackingException(ex);
        }
        catch (DbUpdateException ex) when (IsOneActivationViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();
            throw new PledgeAlreadyActivatedException(ex);
        }
        catch (DbUpdateException ex) when (IsOnePayoutViolation(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _dbContext.ChangeTracker.Clear();
            throw new PayoutAlreadyExistsException(ex);
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

    private static bool IsOneLivePledgeViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PledgeIndexes.OneLivePledge
        };

    private static bool IsOneActivationViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PledgeIndexes.OneActivationPerPledge
        };

    private static bool IsOnePayoutViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PledgeIndexes.OnePayoutPerCampaign
        };

    private static bool IsIdempotencyKeyViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PledgeIndexes.IdempotencyKey
        };

    private static OutboxMessage MapApplicationEvent(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            PledgePaymentConfirmedDomainEvent @event => OutboxMessage.Create(
                new PledgePaymentConfirmedApplicationEvent(
                    @event.PledgeId,
                    @event.CampaignId,
                    @event.BackerId,
                    @event.Amount,
                    @event.Currency,
                    @event.RewardTierReservationId),
                DateTime.UtcNow),
            PledgeActivatedDomainEvent @event => OutboxMessage.Create(
                new PledgeActivatedApplicationEvent(
                    @event.PledgeId, @event.CampaignId, @event.BackerId, @event.Amount, @event.Currency, @event.RewardTierReservationId),
                DateTime.UtcNow),
            PledgeReleasedDomainEvent @event => OutboxMessage.Create(
                new PledgeReleasedApplicationEvent(
                    @event.PledgeId, @event.CampaignId, @event.BackerId, @event.Amount, @event.Currency, @event.RewardTierReservationId),
                DateTime.UtcNow),
            PledgeRefundedDomainEvent @event => OutboxMessage.Create(
                new PledgeRefundedApplicationEvent(
                    @event.PledgeId,
                    @event.CampaignId,
                    @event.BackerId,
                    @event.Amount,
                    @event.Currency,
                    @event.RewardTierReservationId),
                DateTime.UtcNow),
            PledgePaymentFailedDomainEvent @event => OutboxMessage.Create(
                new PledgePaymentFailedApplicationEvent(
                    @event.PledgeId,
                    @event.CampaignId,
                    @event.BackerId,
                    @event.Amount,
                    @event.Currency,
                    @event.FailureReason,
                    @event.RewardTierReservationId),
                DateTime.UtcNow),
            // Fail loud instead of silently discarding (improvement.md §2.3): an unmapped domain
            // event throws immediately rather than vanishing with no log and no side effect.
            _ => throw new InvalidOperationException(
                $"No outbox mapping is registered for domain event '{domainEvent.GetType().Name}'.")
        };
    }
}
