using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;

public sealed class CampaignTransactionExecutor : ICampaignTransactionExecutor
{
    private readonly CampaignsDbContext _dbContext;

    public CampaignTransactionExecutor(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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
            var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
            var outboxMessages = domainEvents.Select(MapApplicationEvent).Where(message => message is not null).Cast<OutboxMessage>().ToArray();

            if (outboxMessages.Length > 0)
            {
                await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            DomainEventAccessor.ClearDomainEvents(_dbContext);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
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

    private static OutboxMessage? MapApplicationEvent(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            CampaignCreatedDomainEvent @event => OutboxMessage.Create(
                new CampaignCreatedApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignPublishedDomainEvent @event => OutboxMessage.Create(
                new CampaignPublishedApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignCancelledDomainEvent @event => OutboxMessage.Create(
                new CampaignCancelledApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            _ => null
        };
    }
}