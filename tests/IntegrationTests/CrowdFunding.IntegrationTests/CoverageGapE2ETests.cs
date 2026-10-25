using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-016: closes the specific zero/low-coverage HTTP surface identified by the coverage
/// audit — list/pagination endpoints, the permissions-grant endpoint, health checks, Moderation
/// rejection, and 404 negative paths — none of which any other E2E suite happens to exercise.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class CoverageGapE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public CoverageGapE2ETests(CrowdFundingApiFactory factory)
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
    public async Task HealthLive_ShouldReturnHealthyWithNoChecksRun()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<HealthCheckDocument>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task HealthReady_ShouldReturnHealthyWithEveryModuleDbContextChecked()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<HealthCheckDocument>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        Assert.Contains(body.Checks, check => check.Name == "campaigns-db" && check.Status == "Healthy");
        Assert.Contains(body.Checks, check => check.Name == "pledges-db" && check.Status == "Healthy");
        Assert.Contains(body.Checks, check => check.Name == "identity-db" && check.Status == "Healthy");
        Assert.Contains(body.Checks, check => check.Name == "moderation-db" && check.Status == "Healthy");
    }

    private sealed record HealthCheckDocument(string Status, HealthCheckEntry[] Checks, double TotalDurationMs);

    private sealed record HealthCheckEntry(string Name, string Status, string? Description);

    [Fact]
    public async Task ListCampaigns_ShouldReturnPagedResponseContainingCreatedCampaign()
    {
        using var client = _factory.CreateClient();
        var (ownerId, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-campaigns-owner"));
        client.SetBearerToken(token);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Listable Campaign"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();

        var listResponse = await client.GetAsync($"/api/campaigns?ownerId={ownerId}&pageNumber=1&pageSize=50");
        listResponse.EnsureSuccessStatusCode();

        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ListCampaignsResponse>>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.PageNumber);
        Assert.Contains(page.Items, item => item.Id == created!.CampaignId);
    }

    [Fact]
    public async Task ListPledgesByCampaign_ShouldReturnPagedResponseContainingMadePledge()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-pledges-owner"));
        client.SetBearerToken(token);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Campaign With Pledges"));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("list-pledges-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "List Pledges Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: "Approved."));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(token);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxMessagesAsync();

        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-pledges-backer"));
        client.SetBearerToken(backerToken);

        var makeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(100m, "USD"));
        makeResponse.EnsureSuccessStatusCode();
        var pledge = await makeResponse.Content.ReadFromJsonAsync<MakePledgeResponse>();

        var listResponse = await client.GetAsync($"/api/campaigns/{campaignId}/pledges?pageNumber=1&pageSize=50");
        listResponse.EnsureSuccessStatusCode();

        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ListPledgesResponse>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == pledge!.PledgeId);
    }

    [Fact]
    public async Task ListModerationReviews_ByModerator_ShouldReturnPagedResponse()
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-reviews-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Campaign Awaiting Review"));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var moderatorEmail = ApiTestExtensions.UniqueEmail("list-reviews-moderator");
        await AdminSeeder.RunAsync(_factory.Services, moderatorEmail, ApiTestExtensions.DefaultPassword, "List Reviews Moderator");
        var moderatorToken = await client.LoginUserAsync(moderatorEmail);
        client.SetBearerToken(moderatorToken);

        // Every test shares one database and reviews are listed oldest first, so the new review may be on a later
        // page; walk the pages instead of assuming it is on the first.
        var found = false;
        for (var pageNumber = 1; pageNumber <= 100 && !found; pageNumber++)
        {
            var listResponse = await client.GetAsync($"/api/moderation/reviews?pageNumber={pageNumber}&pageSize=50");
            listResponse.EnsureSuccessStatusCode();

            var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<CampaignReviewResponse>>();
            Assert.NotNull(page);
            found = page!.Items.Any(item => item.CampaignId == campaignId);
            if (page.Items.Count < 50)
            {
                break;
            }
        }

        Assert.True(found, "The new review was not listed on any page.");
    }

    [Fact]
    public async Task ListModerationReviews_WithoutPermission_ShouldReturnForbidden()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-reviews-no-permission"));
        client.SetBearerToken(token);

        var response = await client.GetAsync("/api/moderation/reviews");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RejectCampaignReview_ByModerator_ShouldTransitionReviewToRejected()
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("reject-review-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Campaign To Reject"));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var moderatorEmail = ApiTestExtensions.UniqueEmail("reject-review-moderator");
        await AdminSeeder.RunAsync(_factory.Services, moderatorEmail, ApiTestExtensions.DefaultPassword, "Reject Review Moderator");
        var moderatorToken = await client.LoginUserAsync(moderatorEmail);
        client.SetBearerToken(moderatorToken);

        var rejectResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/reject", new ReviewCampaignRequest(Notes: "Story needs more detail."));
        rejectResponse.EnsureSuccessStatusCode();

        var rejected = await rejectResponse.Content.ReadFromJsonAsync<CampaignReviewResponse>();
        Assert.Equal("Rejected", rejected!.Status);
    }

    [Fact]
    public async Task GrantPermission_ByAdmin_ShouldAddPermissionToTargetUser()
    {
        using var client = _factory.CreateClient();

        var adminEmail = ApiTestExtensions.UniqueEmail("grant-permission-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Grant Permission Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        var targetEmail = ApiTestExtensions.UniqueEmail("grant-permission-target");
        var (targetUserId, _) = await client.RegisterAndLoginAsync(targetEmail, "Grant Permission Target");

        client.SetBearerToken(adminToken);
        var response = await client.PostAsJsonAsync(
            $"/api/identity/users/{targetUserId}/permissions",
            new GrantPermissionToUserRequest(PermissionConstants.ModerationReview));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GrantPermissionToUserResponse>();
        Assert.NotNull(body);
        Assert.Equal(targetUserId, body!.UserId);
        Assert.Contains(PermissionConstants.ModerationReview, body.ExplicitPermissions);
        Assert.Contains(PermissionConstants.ModerationReview, body.Permissions);
        Assert.Contains(PermissionConstants.CampaignsCreate, body.Permissions);
    }

    [Fact]
    public async Task GrantPermission_WithoutPermission_ShouldReturnForbidden()
    {
        using var client = _factory.CreateClient();

        var callerEmail = ApiTestExtensions.UniqueEmail("grant-permission-no-permission-caller");
        var (_, callerToken) = await client.RegisterAndLoginAsync(callerEmail, "No Permission Caller");

        var targetEmail = ApiTestExtensions.UniqueEmail("grant-permission-no-permission-target");
        var (targetUserId, _) = await client.RegisterAndLoginAsync(targetEmail, "No Permission Target");

        client.SetBearerToken(callerToken);
        var response = await client.PostAsJsonAsync(
            $"/api/identity/users/{targetUserId}/permissions",
            new GrantPermissionToUserRequest(PermissionConstants.ModerationReview));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCampaignById_WithUnknownId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublishCampaign_WithUnknownId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("publish-unknown-campaign"));
        client.SetBearerToken(token);

        var response = await client.PostAsync($"/api/campaigns/{Guid.NewGuid()}/publish", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPledgeById_WithUnknownId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("get-pledge-unknown"), "Backer");
        client.SetBearerToken(token);

        var response = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}/pledges/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetModerationReviewByCampaignId_WithUnknownCampaign_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var moderatorEmail = ApiTestExtensions.UniqueEmail("get-review-unknown-moderator");
        await AdminSeeder.RunAsync(_factory.Services, moderatorEmail, ApiTestExtensions.DefaultPassword, "Get Review Unknown Moderator");
        var moderatorToken = await client.LoginUserAsync(moderatorEmail);
        client.SetBearerToken(moderatorToken);

        var response = await client.GetAsync($"/api/moderation/reviews/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
