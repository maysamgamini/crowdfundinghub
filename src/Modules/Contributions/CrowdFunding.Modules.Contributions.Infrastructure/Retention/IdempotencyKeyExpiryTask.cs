using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Retention;

/// <summary>
/// Clears the idempotency key of pledges older than the retention window. A key only has to protect against
/// a client retrying the same request; a pledge a week old is not a retry. Clearing it frees the unique
/// index entry, so the table does not carry keys for ever, and the pledge itself is untouched.
/// </summary>
public sealed class IdempotencyKeyExpiryTask(IOptions<RetentionOptions> options) : IRetentionTask
{
    public string Name => "idempotency-key-expiry";

    public async Task<int> RunAsync(IServiceProvider scopedServices, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = nowUtc.AddDays(-options.Value.IdempotencyKeyDays);
        var dbContext = scopedServices.GetRequiredService<ContributionsDbContext>();
        var entity = dbContext.Model.FindEntityType(typeof(Contribution))!;
        var table = $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"";

#pragma warning disable EF1002 // the table name comes from the EF model, never from user input
        return await dbContext.Database.ExecuteSqlRawAsync(
            $"UPDATE {table} SET \"IdempotencyKey\" = NULL WHERE \"IdempotencyKey\" IS NOT NULL AND \"CreatedAtUtc\" < {{0}}",
            [cutoff],
            cancellationToken);
#pragma warning restore EF1002
    }
}
