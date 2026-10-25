using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IPaymentWebhookIdempotencyStore"/>
public sealed class PaymentWebhookIdempotencyStore : IPaymentWebhookIdempotencyStore
{
    private readonly PledgesDbContext _dbContext;

    public PaymentWebhookIdempotencyStore(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsProcessedAsync(string webhookEventId, CancellationToken cancellationToken)
        => _dbContext.ProcessedPaymentWebhooks.AnyAsync(x => x.WebhookEventId == webhookEventId, cancellationToken);

    public Task MarkProcessedAsync(string webhookEventId, string paymentIntentId, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        _dbContext.ProcessedPaymentWebhooks.Add(new ProcessedPaymentWebhook
        {
            WebhookEventId = webhookEventId,
            PaymentIntentId = paymentIntentId,
            ProcessedAtUtc = processedAtUtc,
        });

        return Task.CompletedTask;
    }
}
