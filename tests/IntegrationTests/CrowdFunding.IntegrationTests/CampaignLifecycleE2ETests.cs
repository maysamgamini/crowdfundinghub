using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Drives Create -&gt; outbox-published CampaignReview -&gt; Moderation Approve -&gt; Publish across real
/// HTTP calls, using <see cref="CrowdFundingApiFactory.ProcessOutboxMessagesAsync"/> to advance the
/// outbox deterministically instead of waiting on its 5-second poll.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class CampaignLifecycleE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public CampaignLifecycleE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateCampaignRequest ValidCampaignRequest(string title) => new(
        title,
        "A story that is definitely longer than twenty characters, describing the campaign's purpose.",
        "Technology",
        5000m,
        "USD",
        DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task FullLifecycle_CreateApprovePublish_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var creatorEmail = ApiTestExtensions.UniqueEmail("campaign-creator");
        var (_, creatorToken) = await client.RegisterAndLoginAsync(creatorEmail, "Campaign Creator");
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Solar Lanterns For Villages"));
        createResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;

        // Campaign creation only enqueues an outbox message; the Moderation module's CampaignReview
        // row is created asynchronously by CampaignCreatedApplicationEventHandler.
        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("campaign-moderator");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Campaign Moderator");
        var adminToken = await client.LoginUserAsync(adminEmail);

        client.SetBearerToken(adminToken);
        var reviewResponse = await client.GetAsync($"/api/moderation/reviews/{campaignId}");
        reviewResponse.EnsureSuccessStatusCode();
        var review = await reviewResponse.Content.ReadFromJsonAsync<CampaignReviewResponse>();
        Assert.NotNull(review);
        Assert.Equal(campaignId, review!.CampaignId);

        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: "Looks good."));
        approveResponse.EnsureSuccessStatusCode();
        var approved = await approveResponse.Content.ReadFromJsonAsync<CampaignReviewResponse>();
        Assert.Equal("Approved", approved!.Status);

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();
        var published = await publishResponse.Content.ReadFromJsonAsync<PublishCampaignResponse>();
        Assert.Equal("Published", published!.Status);

        var getResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        getResponse.EnsureSuccessStatusCode();
        var campaign = await getResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal("Published", campaign!.Status);
    }

    [Fact]
    public async Task Publish_BeforeModerationApproval_ShouldReturnConflict()
    {
        using var client = _factory.CreateClient();

        var creatorEmail = ApiTestExtensions.UniqueEmail("unapproved-creator");
        var (_, creatorToken) = await client.RegisterAndLoginAsync(creatorEmail, "Unapproved Creator");
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Community Garden Fund"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();

        // A pending (not yet approved) CampaignReview must exist before publish can even evaluate
        // moderation status — without processing the outbox, no review row exists at all.
        await _factory.ProcessOutboxMessagesAsync();

        var publishResponse = await client.PostAsync($"/api/campaigns/{created!.CampaignId}/publish", content: null);

        Assert.Equal(HttpStatusCode.Conflict, publishResponse.StatusCode);
    }

    [Fact]
    public async Task CreateCampaign_WithShortStory_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();

        var creatorEmail = ApiTestExtensions.UniqueEmail("invalid-story-creator");
        var (_, creatorToken) = await client.RegisterAndLoginAsync(creatorEmail, "Invalid Story Creator");
        client.SetBearerToken(creatorToken);

        var invalidRequest = new CreateCampaignRequest(
            "Too Short Story Campaign", "Too short.", "Technology", 1000m, "USD", DateTime.UtcNow.AddDays(10));

        var response = await client.PostAsJsonAsync("/api/campaigns", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
