using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-027's deadline-driven lifecycle: a published campaign whose deadline passes without
/// reaching its goal transitions to Failed, which in turn triggers the refund saga for every
/// backer whose payment was already confirmed.
///
/// The campaign is seeded directly through the Campaigns module's own repository/transaction
/// executor (Create + Publish called back-to-back on explicit, fabricated timestamps) rather than
/// through the full HTTP create/moderate/publish flow, so the only real-time wait needed is the
/// few seconds between placing a pledge and the deadline actually elapsing —
/// <c>CampaignExpirationBackgroundService</c> and <c>MakeContributionCommandHandler</c>'s deadline
/// guard both compare against the real system clock (<c>SystemDateTimeProvider</c>), so a pledge
/// has to land before the deadline and expiration has to run after it.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class CampaignExpirationE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public CampaignExpirationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExpiredUnderfundedCampaign_ShouldFailAndRefundItsBacker()
    {
        var createdAtUtc = DateTime.UtcNow.AddMinutes(-10);
        var deadlineUtc = DateTime.UtcNow.AddSeconds(4);
        var ownerId = Guid.NewGuid();

        var campaign = Campaign.Create(
            ownerId,
            "Campaign Expiration Test",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology",
            new Money(10_000m, "USD"),
            deadlineUtc,
            createdAtUtc);
        campaign.Publish(createdAtUtc.AddMinutes(1));
        var campaignId = campaign.Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
            var transactionExecutor = scope.ServiceProvider.GetRequiredService<ICampaignTransactionExecutor>();
            await transactionExecutor.ExecuteAsync(ct => repository.AddAsync(campaign, ct), CancellationToken.None);
        }

        // Replicates the campaign (Created + Published) into Contributions' active_campaigns_cache.
        await _factory.ProcessOutboxMessagesAsync();

        using var client = _factory.CreateClient();
        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("expiration-backer"), "Expiration Backer");
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(50m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contribution = await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();

        var adminEmail = ApiTestExtensions.UniqueEmail("expiration-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Expiration Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contribution!.ContributionId}/confirm-payment",
            new ConfirmContributionPaymentRequest("test-payment-ref"));
        confirmResponse.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        // The deadline set above is a genuine few seconds in the future (SystemDateTimeProvider
        // reads the real clock), so the expiration worker's "has it passed?" check needs real
        // time to actually elapse — everything up to this point runs well within that window.
        var remaining = deadlineUtc - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(500));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var expirationService = scope.ServiceProvider
                .GetServices<IHostedService>()
                .OfType<CampaignExpirationBackgroundService>()
                .First();
            await expirationService.ProcessExpiredCampaignsAsync(CancellationToken.None);
        }

        // CampaignFailedApplicationEvent must reach the refund saga.
        await _factory.ProcessOutboxMessagesAsync();

        var campaignResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        campaignResponse.EnsureSuccessStatusCode();
        var campaignAfter = await campaignResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal("Failed", campaignAfter!.Status);

        var contributionResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contribution.ContributionId}");
        contributionResponse.EnsureSuccessStatusCode();
        var contributionDetail = await contributionResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
        Assert.Equal("Refunded", contributionDetail!.Status);
    }
}
