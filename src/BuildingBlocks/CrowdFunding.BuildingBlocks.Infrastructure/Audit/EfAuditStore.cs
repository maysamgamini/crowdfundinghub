using CrowdFunding.BuildingBlocks.Application.Audit;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// Persists <see cref="AuditRecord"/>s directly via <see cref="AuditDbContext"/>. No transaction
/// executor/outbox wrapping is needed here — a single insert into a dedicated table is already
/// atomic, and unlike a module's own aggregate writes, there is no related state in the same
/// database to keep in sync with it.
/// </summary>
public sealed class EfAuditStore : IAuditStore
{
    private readonly AuditDbContext _dbContext;

    public EfAuditStore(AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        _dbContext.AuditRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
