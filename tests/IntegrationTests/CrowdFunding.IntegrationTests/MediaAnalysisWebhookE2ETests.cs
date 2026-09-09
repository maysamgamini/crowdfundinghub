using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-032: proves the asynchronous, signed-webhook media-analysis flow — a campaign's review
/// can be auto-approved (or left for a human) purely by the Cloud Function's callback, with no
/// synchronous AI call anywhere in campaign creation.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class MediaAnalysisWebhookE2ETests
{
    private const string WebhookSecret = "cfsec_dev_local_only_change_in_production";

    private readonly CrowdFundingApiFactory _factory;

    public MediaAnalysisWebhookE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private static string SignHeader(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        return "sha256=" + Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client, Guid campaignId, bool passedSafetyCheck, decimal toxicityScore, decimal adultContentScore, bool signed = true)
    {
        var payload = JsonSerializer.Serialize(new
        {
            campaignId,
            passedSafetyCheck,
            toxicityScore,
            adultContentScore,
            detectedLabels = Array.Empty<string>(),
            analyzedAtUtc = DateTime.UtcNow,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/moderation/media-analysis")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (signed)
        {
            request.Headers.Add("X-Cloud-Signature", SignHeader(payload));
        }

        return await client.SendAsync(request);
    }

    private async Task<Guid> CreateCampaignAwaitingReviewAsync(HttpClient client, string label)
    {
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-creator"));
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Media Analysis Test Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Community",
            5000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var campaignId = (await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        return campaignId;
    }

    private async Task<string> GetReviewStatusAsync(HttpClient client, Guid campaignId)
    {
        var moderatorEmail = ApiTestExtensions.UniqueEmail("media-analysis-moderator");
        await AdminSeeder.RunAsync(_factory.Services, moderatorEmail, ApiTestExtensions.DefaultPassword, "Media Analysis Moderator");
        var moderatorToken = await client.LoginUserAsync(moderatorEmail);
        client.SetBearerToken(moderatorToken);

        var response = await client.GetAsync($"/api/moderation/reviews/{campaignId}");
        response.EnsureSuccessStatusCode();
        var review = await response.Content.ReadFromJsonAsync<CampaignReviewResponse>();
        return review!.Status;
    }

    [Fact]
    public async Task PassingAnalysis_ShouldAutoApproveTheReview_WithNoHumanModerator()
    {
        using var client = _factory.CreateClient();
        var campaignId = await CreateCampaignAwaitingReviewAsync(client, "auto-approve");

        var response = await PostWebhookAsync(client, campaignId, passedSafetyCheck: true, toxicityScore: 0.02m, adultContentScore: 0.01m);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Approved", await GetReviewStatusAsync(client, campaignId));
    }

    [Fact]
    public async Task FailingAnalysis_ShouldLeaveTheReviewPending_ForHumanModeration()
    {
        using var client = _factory.CreateClient();
        var campaignId = await CreateCampaignAwaitingReviewAsync(client, "needs-human");

        var response = await PostWebhookAsync(client, campaignId, passedSafetyCheck: false, toxicityScore: 0.87m, adultContentScore: 0.05m);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Pending", await GetReviewStatusAsync(client, campaignId));
    }

    [Fact]
    public async Task WebhookWithoutASignature_ShouldBeRejected()
    {
        using var client = _factory.CreateClient();
        var campaignId = await CreateCampaignAwaitingReviewAsync(client, "unsigned");

        var response = await PostWebhookAsync(client, campaignId, true, 0.01m, 0.01m, signed: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WebhookForAnUnknownCampaign_ShouldReturnNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await PostWebhookAsync(client, Guid.NewGuid(), true, 0.01m, 0.01m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnalysisArrivingAfterAHumanAlreadyApproved_ShouldBeIgnoredNotOverwritten()
    {
        using var client = _factory.CreateClient();
        var campaignId = await CreateCampaignAwaitingReviewAsync(client, "already-human-approved");

        var adminEmail = ApiTestExtensions.UniqueEmail("already-approved-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Already Approved Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: "Manually approved before the AI callback landed."));
        approveResponse.EnsureSuccessStatusCode();

        // The slow (3-15s) analysis lands after a human already acted — must not throw and must
        // not clobber the human moderator's decision.
        var webhookResponse = await PostWebhookAsync(client, campaignId, passedSafetyCheck: false, toxicityScore: 0.99m, adultContentScore: 0.99m);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        Assert.Equal("Approved", await GetReviewStatusAsync(client, campaignId));
    }
}
