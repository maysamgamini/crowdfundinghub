using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The webhook dispatcher loads a subscription, makes an HTTP call of up to five seconds, then
/// saves the subscription back. If the campaign owner deletes (deactivates) the subscription in
/// that window, the dispatcher's save must not switch it back on.
/// </summary>
public sealed class WebhookSubscriptionConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("webhook_subscriptions")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task OwnerDeletingDuringADelivery_IsNotUndoneByTheDispatchersSave()
    {
        var subscription = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/hook", "secret", DateTime.UtcNow);
        await using (var seed = CreateContext())
        {
            await new WebhookSubscriptionRepository(seed).AddAsync(subscription, CancellationToken.None);
        }

        // The dispatcher loads the subscription and starts its HTTP call.
        await using var dispatcherContext = CreateContext();
        var dispatcherRepository = new WebhookSubscriptionRepository(dispatcherContext);
        var loadedByDispatcher = await dispatcherRepository.GetByIdAsync(subscription.Id, CancellationToken.None);

        // Meanwhile the owner deletes the subscription and that commits.
        await using (var ownerContext = CreateContext())
        {
            var ownerRepository = new WebhookSubscriptionRepository(ownerContext);
            var loadedByOwner = await ownerRepository.GetByIdAsync(subscription.Id, CancellationToken.None);
            loadedByOwner!.Deactivate();
            await ownerRepository.UpdateAsync(loadedByOwner, CancellationToken.None);
        }

        // The delivery finishes and the dispatcher records the outcome.
        loadedByDispatcher!.RecordDeliverySuccess();
        try
        {
            await dispatcherRepository.UpdateAsync(loadedByDispatcher, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Acceptable: the stale write is refused rather than applied.
        }

        await using var verify = CreateContext();
        var stored = await verify.WebhookSubscriptions.AsNoTracking().SingleAsync(x => x.Id == subscription.Id);
        Assert.False(stored.IsActive);
    }

    private CampaignUpdatesDbContext CreateContext()
        => new(new DbContextOptionsBuilder<CampaignUpdatesDbContext>()
            .UseNpgsql(_container.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "campaignupdates"))
            .Options);
}
