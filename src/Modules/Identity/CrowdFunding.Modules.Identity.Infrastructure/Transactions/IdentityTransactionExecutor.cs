using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.Modules.Identity.Infrastructure.Transactions;

/// <summary>
/// Executes the surrounding module write operation inside a transaction boundary. Mirrors
/// CampaignTransactionExecutor/ContributionTransactionExecutor/ModerationTransactionExecutor,
/// minus outbox/domain-event harvesting — the Identity module doesn't raise domain events today.
/// </summary>
public sealed class IdentityTransactionExecutor : IIdentityTransactionExecutor
{
    private readonly IdentityDbContext _dbContext;

    public IdentityTransactionExecutor(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
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
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
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
            // can catch and retry without taking a dependency on Entity Framework Core. Mirrors
            // CampaignTransactionExecutor/ContributionTransactionExecutor.
            throw new ConcurrencyConflictException(
                "The entity was modified by another transaction. Retry with a fresh read.", ex);
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
}
