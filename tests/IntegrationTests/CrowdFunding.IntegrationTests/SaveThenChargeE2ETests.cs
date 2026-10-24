using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.BackgroundServices;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Payments;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The save-then-charge funding model end to end. A pledge saves a card and counts as a promise; nothing is
/// charged until the deadline, and then only if the campaign was funded. These tests run the whole path against a
/// real database with the fake provider: promise, deadline, one charge per pledge, a decline that is retried as a
/// new attempt, and release without any charge when the campaign fails.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class SaveThenChargeE2ETests
{
    private readonly CrowdFundingApiFactory _baseFactory;

    public SaveThenChargeE2ETests(CrowdFundingApiFactory factory)
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

    /// <summary>Runs one pass of every module's outbox worker, and the settlement worker, on this host.</summary>
    private static async Task DeliverAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        foreach (var dispatcher in scope.ServiceProvider.GetServices<IHostedService>().OfType<IOutboxDispatcher>())
        {
            await dispatcher.ProcessBatchAsync(CancellationToken.None);
        }
    }

    private sealed record Pledge(Guid CampaignId, Guid Id, string Card);

    private static async Task<Guid> SeedCampaignAsync(IServiceProvider services, decimal goal, TimeSpan deadlineIn)
    {
        var createdAtUtc = DateTime.UtcNow.AddMinutes(-10);
        var campaign = Campaign.Create(
            Guid.NewGuid(), "Save Then Charge Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology", new Money(goal, "USD"), DateTime.UtcNow.Add(deadlineIn), createdAtUtc);
        campaign.Publish(createdAtUtc.AddMinutes(1));

        using var scope = services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ICampaignTransactionExecutor>();
        await executor.ExecuteAsync(ct => repository.AddAsync(campaign, ct), CancellationToken.None);
        return campaign.Id;
    }

    private static async Task<Pledge> PledgeAsync(
        HttpClient client, Guid campaignId, string adminToken, string label, string card, bool saveCard)
    {
        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail(label), label);
        client.SetBearerToken(backerToken);
        var response = await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD"));
        response.EnsureSuccessStatusCode();
        var made = (await response.Content.ReadFromJsonAsync<MakeContributionResponse>())!;
        Assert.False(string.IsNullOrEmpty(made.ClientSecret));

        if (saveCard)
        {
            client.SetBearerToken(adminToken);
            (await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/contributions/{made.ContributionId}/confirm-setup",
                new ConfirmPledgeSetupRequest(card))).EnsureSuccessStatusCode();
        }

        return new Pledge(campaignId, made.ContributionId, card);
    }

    private static async Task<ContributionStatus> StatusAsync(IServiceProvider services, Guid pledgeId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        return (await db.Contributions.AsNoTracking().SingleAsync(c => c.Id == pledgeId)).Status;
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

    [Fact]
    public async Task AFundedCampaign_ChargesEachSavedCardOnce_RetriesADeclineAsANewAttempt_AndReleasesTheUnsavedPledge()
    {
        await using var host = SaveThenChargeHost();
        using var client = host.CreateClient();
        var services = host.Services;
        var provider = host.Services.GetRequiredService<FakePaymentProvider>();

        var deadline = DateTime.UtcNow.AddSeconds(8);
        var campaignId = await SeedCampaignAsync(services, goal: 150m, deadlineIn: deadline - DateTime.UtcNow);
        await DeliverAsync(services);

        var adminEmail = ApiTestExtensions.UniqueEmail("stc-admin");
        await AdminSeeder.RunAsync(services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        var good = await PledgeAsync(client, campaignId, adminToken, "stc-good", "pm_ok_1", saveCard: true);
        var flaky = await PledgeAsync(client, campaignId, adminToken, "stc-flaky", "pm_decline_once_1", saveCard: true);
        var unsaved = await PledgeAsync(client, campaignId, adminToken, "stc-unsaved", "n/a", saveCard: false);
        await DeliverAsync(services);

        // Nothing is taken while the campaign is open; only the two saved cards count.
        Assert.Equal(0, provider.ChargeCalls);
        Assert.Equal(ContributionStatus.Active, await StatusAsync(services, good.Id));
        Assert.Equal(ContributionStatus.Pending, await StatusAsync(services, unsaved.Id));

        await CloseAtDeadlineAsync(services, deadline);
        await DeliverAsync(services);   // CampaignSucceeded -> pledges queued; the settlement pass charges them
        await DeliverAsync(services);

        Assert.Equal(ContributionStatus.Succeeded, await StatusAsync(services, good.Id));
        Assert.Equal(ContributionStatus.ToCharge, await StatusAsync(services, flaky.Id));
        Assert.Equal(ContributionStatus.Released, await StatusAsync(services, unsaved.Id));

        // The decline is retried later as a new attempt (new key). Make it due now instead of waiting a day.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE contributions.contributions SET \"NextChargeAtUtc\" = {0} WHERE \"Id\" = {1}",
                DateTime.UtcNow.AddMinutes(-1), flaky.Id);
        }

        await DeliverAsync(services);
        await DeliverAsync(services);

        Assert.Equal(ContributionStatus.Succeeded, await StatusAsync(services, flaky.Id));
        Assert.Equal(2, provider.ChargesPerformed);
        Assert.Contains(provider.ChargeKeys, k => k.Contains($"{flaky.Id:N}:charge:2"));

        // Running the settlement again must not charge anyone twice.
        var callsBefore = provider.ChargeCalls;
        await DeliverAsync(services);
        Assert.Equal(callsBefore, provider.ChargeCalls);
    }

    [Fact]
    public async Task AFailedCampaign_ReleasesEveryPledge_AndChargesNobody()
    {
        await using var host = SaveThenChargeHost();
        using var client = host.CreateClient();
        var services = host.Services;
        var provider = services.GetRequiredService<FakePaymentProvider>();

        var deadline = DateTime.UtcNow.AddSeconds(6);
        var campaignId = await SeedCampaignAsync(services, goal: 10_000m, deadlineIn: deadline - DateTime.UtcNow);
        await DeliverAsync(services);

        var adminEmail = ApiTestExtensions.UniqueEmail("stc-fail-admin");
        await AdminSeeder.RunAsync(services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        var promised = await PledgeAsync(client, campaignId, adminToken, "stc-promised", "pm_ok_2", saveCard: true);
        await DeliverAsync(services);
        Assert.Equal(ContributionStatus.Active, await StatusAsync(services, promised.Id));

        await CloseAtDeadlineAsync(services, deadline);
        await DeliverAsync(services);
        await DeliverAsync(services);

        Assert.Equal(ContributionStatus.Released, await StatusAsync(services, promised.Id));
        Assert.Equal(0, provider.ChargeCalls);
        Assert.Equal(0, provider.RefundCalls);
    }
}
