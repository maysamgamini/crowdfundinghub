using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Retention;

/// <summary>
/// Deletes the records of applied payment webhook event ids older than the retention, so the table does not grow
/// for ever. A record only has to outlive the longest time the provider can still redeliver the same event.
/// </summary>
public sealed class PaymentWebhookPruneTask(IOptions<RetentionOptions> options) : IRetentionTask
{
    public string Name => "payment-webhook-prune";

    public async Task<int> RunAsync(IServiceProvider scopedServices, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = nowUtc.AddDays(-options.Value.PaymentWebhookDays);
        var dbContext = scopedServices.GetRequiredService<PledgesDbContext>();

        return await dbContext.ProcessedPaymentWebhooks
            .Where(webhook => webhook.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
