using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.RewardTiers;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-034: proves the reward-tier reservation invariant
/// (<c>ClaimedCount + ReservedCount &lt;= TotalCapacity</c>) holds under real concurrent load
/// against real PostgreSQL — not just in the single-threaded domain unit tests.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RewardTierConcurrencyE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public RewardTierConcurrencyE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FiftyConcurrentReservations_AgainstFiveRemainingSlots_ShouldYieldExactlyFiveSuccesses()
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("reward-tier-creator"));
        client.SetBearerToken(creatorToken);

        var createCampaignResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Reward Tier Concurrency Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology",
            50_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createCampaignResponse.EnsureSuccessStatusCode();
        var campaignId = (await createCampaignResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        var createTierResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/reward-tiers",
            new CreateRewardTierRequest("Early Bird", "Limited edition", 199m, "USD", TotalCapacity: 5));
        createTierResponse.EnsureSuccessStatusCode();
        var rewardTierId = (await createTierResponse.Content.ReadFromJsonAsync<CreateRewardTierResponse>())!.RewardTierId;

        // 50 distinct backers race for the last 5 slots — each with its own HttpClient/bearer
        // token, so this is a genuine concurrent-request race, not 50 calls serialized behind one
        // shared client.
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < 50; i++)
        {
            tasks.Add(ReserveAsync(campaignId, rewardTierId, $"reward-tier-backer-{i}"));
        }

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejected = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        Assert.Equal(5, succeeded);
        Assert.Equal(45, rejected);
        Assert.Equal(50, succeeded + rejected);

        var finalCheckClient = _factory.CreateClient();
        finalCheckClient.SetBearerToken(creatorToken);
        var moderatorResponse = await finalCheckClient.PostAsync(
            $"/api/campaigns/{campaignId}/reward-tiers/{rewardTierId}/reserve", content: null);
        // The tier is now fully exhausted — even a 51st attempt is rejected the same way.
        Assert.Equal(HttpStatusCode.BadRequest, moderatorResponse.StatusCode);
    }

    private async Task<HttpResponseMessage> ReserveAsync(Guid campaignId, Guid rewardTierId, string label)
    {
        using var backerClient = _factory.CreateClient();
        var (_, backerToken) = await backerClient.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail(label));
        backerClient.SetBearerToken(backerToken);

        return await backerClient.PostAsync($"/api/campaigns/{campaignId}/reward-tiers/{rewardTierId}/reserve", content: null);
    }
}
