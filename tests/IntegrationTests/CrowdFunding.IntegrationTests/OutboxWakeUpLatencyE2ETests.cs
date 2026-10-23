using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Without a wake-up signal an outbox worker only looks for new rows on its timer tick (every five
/// seconds here), so an event can wait that long before anything reacts to it. These tests never
/// drain the outbox by hand: they create a campaign and wait for the Moderation module to react to
/// the CampaignCreated event, which only happens after the Campaigns worker delivers it.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OutboxWakeUpLatencyE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public OutboxWakeUpLatencyE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnEvent_ShouldReachItsConsumerWellInsideTheFallbackPollInterval_WithoutAnyManualDrain()
    {
        using var creator = _factory.CreateClient();
        var (_, creatorToken) = await creator.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("latency-creator"), "Creator");

        var adminEmail = ApiTestExtensions.UniqueEmail("latency-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        using var admin = _factory.CreateClient();
        admin.SetBearerToken(await admin.LoginUserAsync(adminEmail));

        // Five samples: if delivery waited for the five second timer, the chance that every sample
        // lands under 1.5 seconds is about (1.5 / 5) ^ 5, well under one percent.
        var slowest = TimeSpan.Zero;
        for (var sample = 0; sample < 5; sample++)
        {
            creator.SetBearerToken(creatorToken);
            var response = await creator.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
                $"Wake-up latency sample {sample}",
                "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
                "Community", 5_000m, "USD", DateTime.UtcNow.AddDays(30)));
            response.EnsureSuccessStatusCode();
            var campaignId = (await response.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

            var clock = Stopwatch.StartNew();
            var reacted = false;
            while (clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                var review = await admin.GetAsync($"/api/moderation/reviews/{campaignId}");
                if (review.StatusCode == HttpStatusCode.OK)
                {
                    reacted = true;
                    break;
                }

                await Task.Delay(20);
            }

            Assert.True(reacted, $"Moderation never reacted to campaign {campaignId} within 10 seconds.");
            slowest = clock.Elapsed > slowest ? clock.Elapsed : slowest;
        }

        Assert.True(slowest < TimeSpan.FromSeconds(1.5), $"The slowest delivery took {slowest.TotalSeconds:F2}s; the wake-up signal should make it near-instant.");
    }
}
