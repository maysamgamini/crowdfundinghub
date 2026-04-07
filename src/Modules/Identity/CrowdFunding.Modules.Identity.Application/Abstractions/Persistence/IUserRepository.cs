using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> AnyAsync(CancellationToken cancellationToken);
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
