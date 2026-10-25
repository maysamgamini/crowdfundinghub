using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// A pledge request that is retried (a timeout, a dropped connection, a double click) must not create
/// a second pledge. These tests run against real PostgreSQL because the guarantee is a unique index,
/// not application code: two requests carrying the same key at the same instant cannot both insert.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PledgeIdempotencyE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public PledgeIdempotencyE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> CreatePublishedCampaignAsync(string label)
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"), "Creator");
        client.SetBearerToken(creatorToken);

        var created = await (await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Idempotent Pledges Campaign",
            "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
            "Community", 10_000m, "USD", DateTime.UtcNow.AddDays(30))).EnsureSuccess())
            .Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;
        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        client.SetBearerToken(await client.LoginUserAsync(adminEmail));
        (await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null))).EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        (await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();
        return campaignId;
    }

    private async Task<string> NewBackerTokenAsync(string label)
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail(label), "Backer");
        return token;
    }

    private Task<HttpResponseMessage> PledgeAsync(Guid campaignId, string token, decimal amount, string? key)
    {
        var client = _factory.CreateClient();
        client.SetBearerToken(token);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/campaigns/{campaignId}/pledges")
        {
            Content = JsonContent.Create(new MakePledgeRequest(amount, "USD"))
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    private async Task<int> CountPledgesAsync(Guid campaignId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        return await db.Pledges.CountAsync(x => x.CampaignId == campaignId);
    }

    [Fact]
    public async Task RetryWithTheSameKey_ShouldReturnTheFirstPledge_AndCreateNoSecondOne()
    {
        var campaignId = await CreatePublishedCampaignAsync("retry");
        var token = await NewBackerTokenAsync("retry-backer");
        var key = Guid.NewGuid().ToString("N");

        var first = await PledgeAsync(campaignId, token, 100m, key);
        var second = await PledgeAsync(campaignId, token, 100m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));
        var firstBody = await first.Content.ReadFromJsonAsync<MakePledgeResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<MakePledgeResponse>();
        Assert.Equal(firstBody!.PledgeId, secondBody!.PledgeId);
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task TwelveSimultaneousRequestsWithTheSameKey_ShouldCreateExactlyOnePledge()
    {
        var campaignId = await CreatePublishedCampaignAsync("race");
        var token = await NewBackerTokenAsync("race-backer");
        var key = Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => PledgeAsync(campaignId, token, 100m, key)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(11, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            ids.Add((await response.Content.ReadFromJsonAsync<MakePledgeResponse>())!.PledgeId);
        }

        Assert.Single(ids);
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task SameKeyWithADifferentAmount_ShouldBeRefusedWithAConflict()
    {
        var campaignId = await CreatePublishedCampaignAsync("mismatch");
        var token = await NewBackerTokenAsync("mismatch-backer");
        var key = Guid.NewGuid().ToString("N");

        (await PledgeAsync(campaignId, token, 100m, key)).EnsureSuccessStatusCode();
        var second = await PledgeAsync(campaignId, token, 999m, key);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task TheSameKeyFromTwoBackers_ShouldCreateTwoPledges()
    {
        var campaignId = await CreatePublishedCampaignAsync("two-backers");
        var key = Guid.NewGuid().ToString("N");

        var one = await PledgeAsync(campaignId, await NewBackerTokenAsync("backer-one"), 100m, key);
        var two = await PledgeAsync(campaignId, await NewBackerTokenAsync("backer-two"), 100m, key);

        Assert.Equal(HttpStatusCode.Created, one.StatusCode);
        Assert.Equal(HttpStatusCode.Created, two.StatusCode);
        Assert.Equal(2, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task ASecondRequestWithoutAKey_IsRefusedByTheOneLivePledgeRule()
    {
        var campaignId = await CreatePublishedCampaignAsync("no-key");
        var token = await NewBackerTokenAsync("no-key-backer");

        (await PledgeAsync(campaignId, token, 100m, key: null)).EnsureSuccessStatusCode();
        var second = await PledgeAsync(campaignId, token, 100m, key: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Theory]
    [InlineData("has a space")]
    [InlineData("café")]
    public async Task AMalformedKey_ShouldBeRejectedBeforeAnyPledgeExists(string key)
    {
        var campaignId = await CreatePublishedCampaignAsync("bad-key");
        var token = await NewBackerTokenAsync("bad-key-backer");

        var response = await PledgeAsync(campaignId, token, 100m, key);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest);
        Assert.Equal(0, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task ASecondPledgeWithANewKey_IsRefusedWith409_WhileTheFirstIsLive()
    {
        var campaignId = await CreatePublishedCampaignAsync("two-tabs");
        var token = await NewBackerTokenAsync("two-tabs-backer");

        (await PledgeAsync(campaignId, token, 100m, Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
        var second = await PledgeAsync(campaignId, token, 100m, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task TwelveSimultaneousPledgesWithTwelveDifferentKeys_CreateExactlyOne()
    {
        var campaignId = await CreatePublishedCampaignAsync("tabs-race");
        var token = await NewBackerTokenAsync("tabs-race-backer");

        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => PledgeAsync(campaignId, token, 100m, Guid.NewGuid().ToString("N"))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(11, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await CountPledgesAsync(campaignId));
    }

    [Fact]
    public async Task AfterTheFirstPledgeFailed_TheSameBackerCanPledgeAgain()
    {
        var campaignId = await CreatePublishedCampaignAsync("after-fail");
        var token = await NewBackerTokenAsync("after-fail-backer");
        var first = await (await PledgeAsync(campaignId, token, 100m, key: null)).Content.ReadFromJsonAsync<MakePledgeResponse>();

        var adminEmail = ApiTestExtensions.UniqueEmail("after-fail-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        using var admin = _factory.CreateClient();
        admin.SetBearerToken(await admin.LoginUserAsync(adminEmail));
        (await admin.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges/{first!.PledgeId}/fail-payment",
            new FailPledgePaymentRequest("card declined"))).EnsureSuccessStatusCode();

        var again = await PledgeAsync(campaignId, token, 100m, key: null);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(2, await CountPledgesAsync(campaignId));
    }
}

internal static class ResponseExtensions
{
    public static async Task<HttpResponseMessage> EnsureSuccess(this Task<HttpResponseMessage> response)
    {
        var result = await response;
        result.EnsureSuccessStatusCode();
        return result;
    }
}
