using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Retention;

/// <summary>Deletes inbox rows older than the retention, so a module's inbox table does not grow forever.</summary>
public sealed class InboxPruneTask<TDbContext>(IOptions<RetentionOptions> options) : IRetentionTask
    where TDbContext : DbContext
{
    public string Name => $"inbox-prune:{typeof(TDbContext).Name}";

    public async Task<int> RunAsync(IServiceProvider scopedServices, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = nowUtc.AddDays(-options.Value.InboxDays);
        var dbContext = scopedServices.GetRequiredService<TDbContext>();

        return await dbContext.Set<InboxMessage>()
            .Where(message => message.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
