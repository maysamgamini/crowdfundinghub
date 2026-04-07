using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .Include(x => x.Roles)
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.Users
            .Include(x => x.Roles)
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Users.AnyAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
