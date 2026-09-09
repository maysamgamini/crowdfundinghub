using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for refresh tokens.
/// </summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, DateTime now, CancellationToken cancellationToken);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
}
