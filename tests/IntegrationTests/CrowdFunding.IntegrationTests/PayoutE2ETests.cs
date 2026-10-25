using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.BackgroundServices;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Paying the creator of a succeeded campaign, end to end against a real database and the fake provider: the
/// amount waits for every charge to settle, the platform fee comes off, the creator is paid once however many
/// times the workers run, and a refused payout is retried under a new key.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PayoutE2ETests
{
    private readonly CrowdFundingApiFactory _baseFactory;

    public PayoutE2ETests(CrowdFundingApiFactory factory)
    {
        _baseFactory = factory;
    }

    private WebApplicationFactory<Program> SaveThenChargeHost()
        => _baseFactory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:FundingModel"] = "SaveThenCharge",
                ["Payments:SettlementPollInterval"] = "00:10:00",
            })));

    private static async Task DeliverAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        foreach (var dispatcher in scope.ServiceProvider.GetServices<IHostedService>().OfType<IOutboxDispatcher>())
        {
            await dispatcher.ProcessBatchAsync(CancellationToken.None);
        }
    }

    private static async Task<Guid> SeedCampaignAsync(IServiceProvider services, Guid ownerId, decimal goal, DateTime deadline)
    {
        var createdAtUtc = DateTime.UtcNow.AddMinutes(-10);
        var campaign = Campaign.Create(
            ownerId, "Payout Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology", new Money(goal, "USD"), deadline, createdAtUtc);
        campaign.Publish(createdAtUtc.AddMinutes(1));

        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ICampaignTransactionExecutor>();
        await executor.ExecuteAsync(ct => repository.AddAsync(campaign, ct), CancellationToken.None);
        return campaign.Id;
    }

    private static async Task<Guid> PledgeAsync(HttpClient client, Guid campaignId, string adminToken, string label, string? card)
    {
        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail(label), label);
        client.SetBearerToken(backerToken);
        var response = await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(100m, "USD"));
        response.EnsureSuccessStatusCode();
        var made = (await response.Content.ReadFromJsonAsync<MakePledgeResponse>())!;

        client.SetBearerToken(adminToken);
        var confirm = card is null
            ? await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/pledges/{made.PledgeId}/confirm-payment", new ConfirmPledgePaymentRequest("payout-test-ref"))
            : await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/pledges/{made.PledgeId}/confirm-setup", new ConfirmPledgeSetupRequest(card));
        confirm.EnsureSuccessStatusCode();
        return made.PledgeId;
    }

    private static async Task CloseAtDeadlineAsync(IServiceProvider services, DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(500));
        }

        using var scope = services.CreateScope();
        var expiration = scope.ServiceProvider.GetServices<IHostedService>().OfType<CampaignExpirationBackgroundService>().First();
        await expiration.ProcessExpiredCampaignsAsync(CancellationToken.None);
    }

    private static async Task<Payout> PayoutOfAsync(IServiceProvider services, Guid campaignId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        return await db.Payouts.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId);
    }

    [Fact]
    public async Task APayout_WaitsForEveryChargeToSettle_ThenPaysTheCreatorNetOfTheFee_Once()
    {
        await using var host = SaveThenChargeHost();
        using var client = host.CreateClient();
        var services = host.Services;
        var provider = services.GetRequiredService<FakePaymentProvider>();
        var ownerId = Guid.NewGuid();

        var deadline = DateTime.UtcNow.AddSeconds(8);
        var campaignId = await SeedCampaignAsync(services, ownerId, goal: 150m, deadline);
        await CrowdFundingApiFactory.DeliverUntilPledgeableAsync(services, campaignId);

        var adminEmail = ApiTestExtensions.UniqueEmail("payout-admin");
        await AdminSeeder.RunAsync(services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        await PledgeAsync(client, campaignId, adminToken, "payout-good", "pm_ok_p1");
        var flakyId = await PledgeAsync(client, campaignId, adminToken, "payout-flaky", "pm_decline_once_p1");
        await DeliverAsync(services);

        await CloseAtDeadlineAsync(services, deadline);
        await DeliverAsync(services);   // CampaignSucceeded: payout recorded, pledges queued
        await DeliverAsync(services);   // one pledge charged, the other declined and waiting for its retry

        // One card was declined, so the amount is not known yet: the creator is not paid.
        var waiting = await PayoutOfAsync(services, campaignId);
        Assert.Equal(PayoutStatus.Pending, waiting.Status);
        Assert.Equal(ownerId, waiting.OwnerId);
        Assert.Equal(0, provider.PayoutCalls);

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE pledges.pledges SET \"NextChargeAtUtc\" = {0} WHERE \"Id\" = {1}", DateTime.UtcNow.AddMinutes(-1), flakyId);
        }

        await DeliverAsync(services);   // the retry charges the second card
        await DeliverAsync(services);   // everything settled: amounts fixed, creator paid

        var paid = await PayoutOfAsync(services, campaignId);
        Assert.Equal(PayoutStatus.Paid, paid.Status);
        Assert.Equal(200m, paid.GrossAmount);
        Assert.Equal(10m, paid.FeeAmount);     // 500 basis points
        Assert.Equal(190m, paid.NetAmount);
        Assert.Equal(1, provider.PayoutsPerformed);
        Assert.Equal(190m, provider.PayoutAmounts[Payout.KeyFor(campaignId, 1)]);

        // Running the workers again, and the same campaign event arriving again, pays nobody twice.
        await DeliverAsync(services);
        await DeliverAsync(services);
        Assert.Equal(1, provider.PayoutsPerformed);
        Assert.Equal(1, provider.PayoutCalls);
    }

    [Fact]
    public async Task ARefusedPayout_IsRetriedLaterUnderANewKey_AndStillPaysOnce()
    {
        await using var host = SaveThenChargeHost();
        using var client = host.CreateClient();
        var services = host.Services;
        var provider = services.GetRequiredService<FakePaymentProvider>();

        var deadline = DateTime.UtcNow.AddSeconds(7);
        var campaignId = await SeedCampaignAsync(services, Guid.NewGuid(), goal: 100m, deadline);
        await CrowdFundingApiFactory.DeliverUntilPledgeableAsync(services, campaignId);

        var adminEmail = ApiTestExtensions.UniqueEmail("payout-retry-admin");
        await AdminSeeder.RunAsync(services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        await PledgeAsync(client, campaignId, adminToken, "payout-retry", "pm_ok_p2");
        await DeliverAsync(services);

        provider.FailPayoutsWith("account_closed");
        await CloseAtDeadlineAsync(services, deadline);
        await DeliverAsync(services);
        await DeliverAsync(services);
        await DeliverAsync(services);

        var refused = await PayoutOfAsync(services, campaignId);
        Assert.Equal(PayoutStatus.Ready, refused.Status);
        Assert.Equal(1, refused.Failures);
        Assert.Equal("account_closed", refused.LastFailureCode);
        Assert.True(refused.NextAttemptAtUtc > DateTime.UtcNow);
        var callsWhileWaiting = provider.PayoutCalls;

        // Until its retry time the worker leaves the payout alone.
        await DeliverAsync(services);
        Assert.Equal(callsWhileWaiting, provider.PayoutCalls);

        provider.FailPayoutsWith(null);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE pledges.payouts SET \"NextAttemptAtUtc\" = {0} WHERE \"CampaignId\" = {1}", DateTime.UtcNow.AddMinutes(-1), campaignId);
        }

        await DeliverAsync(services);

        var paid = await PayoutOfAsync(services, campaignId);
        Assert.Equal(PayoutStatus.Paid, paid.Status);
        Assert.Equal(95m, paid.NetAmount);
        Assert.Equal(1, provider.PayoutsPerformed);
        Assert.Equal(95m, provider.PayoutAmounts[Payout.KeyFor(campaignId, 2)]);
    }

    [Fact]
    public async Task InTheChargeNowModel_TheCreatorIsPaidWhatWasCollected_LessTheFee()
    {
        using var client = _baseFactory.CreateClient();
        var provider = _baseFactory.Services.GetRequiredService<FakePaymentProvider>();
        var paidBefore = provider.PayoutsPerformed;

        var deadline = DateTime.UtcNow.AddSeconds(7);
        var campaignId = await SeedCampaignAsync(_baseFactory.Services, Guid.NewGuid(), goal: 100m, deadline);
        await CrowdFundingApiFactory.DeliverUntilPledgeableAsync(_baseFactory.Services, campaignId);

        var adminEmail = ApiTestExtensions.UniqueEmail("payout-now-admin");
        await AdminSeeder.RunAsync(_baseFactory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        await PledgeAsync(client, campaignId, adminToken, "payout-now", card: null);
        await _baseFactory.ProcessOutboxMessagesAsync();

        await CloseAtDeadlineAsync(_baseFactory.Services, deadline);
        await _baseFactory.ProcessOutboxMessagesAsync();
        await _baseFactory.ProcessOutboxMessagesAsync();

        var paid = await PayoutOfAsync(_baseFactory.Services, campaignId);
        Assert.Equal(PayoutStatus.Paid, paid.Status);
        Assert.Equal(100m, paid.GrossAmount);
        Assert.Equal(95m, paid.NetAmount);
        Assert.Equal(paidBefore + 1, provider.PayoutsPerformed);
    }
}
