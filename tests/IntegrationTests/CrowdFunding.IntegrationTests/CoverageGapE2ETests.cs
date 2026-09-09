using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Contributions;
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
        Assert.Contains(body.Checks, check => check.Name == "contributions-db" && check.Status == "Healthy");
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
    public async Task ListContributionsByCampaign_ShouldReturnPagedResponseContainingMadeContribution()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("list-contributions-owner"));
        client.SetBearerToken(token);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Campaign With Contributions"));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("list-contributions-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "List Contributions Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: "Approved."));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(token);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxMessagesAsync();

        var makeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD"));
        makeResponse.EnsureSuccessStatusCode();
        var contribution = await makeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();

        var listResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions?pageNumber=1&pageSize=50");
        listResponse.EnsureSuccessStatusCode();

        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ListContributionsResponse>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == contribution!.ContributionId);
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

        var listResponse = await client.GetAsync("/api/moderation/reviews?pageNumber=1&pageSize=50");
        listResponse.EnsureSuccessStatusCode();

        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<CampaignReviewResponse>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.CampaignId == campaignId);
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
    public async Task GetContributionById_WithUnknownId_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}/contributions/{Guid.NewGuid()}");

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
