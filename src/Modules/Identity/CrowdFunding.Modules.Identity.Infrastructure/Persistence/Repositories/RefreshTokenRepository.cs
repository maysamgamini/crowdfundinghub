using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Domain.Entities;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements repository operations for RefreshToken.
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _dbContext.RefreshTokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => _dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
        => _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && !x.IsRevoked && x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _dbContext.RefreshTokens.Update(refreshToken);
        return Task.CompletedTask;
    }
}
