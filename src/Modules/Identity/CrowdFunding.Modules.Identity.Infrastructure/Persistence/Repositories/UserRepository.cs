using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for User.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        // Add (not AddAsync) — EF's AddAsync exists only for value generators that need async DB
        // access (e.g. SQL Server HiLo), which User's client-generated Guid key doesn't use.
        // SaveChangesAsync is the transaction executor's job (IIdentityTransactionExecutor), not
        // the repository's — previously this called SaveChangesAsync directly, which committed
        // as soon as this method returned regardless of what else the calling handler intended
        // to do in the same unit of work.
        _dbContext.Users.Add(user);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .Include(x => x.Roles)
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .Include(x => x.Roles)
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Users.AnyAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(user);
        return Task.CompletedTask;
    }
}
