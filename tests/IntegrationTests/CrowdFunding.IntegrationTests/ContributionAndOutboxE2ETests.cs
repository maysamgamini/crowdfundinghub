using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Drives Make Contribution -&gt; Confirm Payment -&gt; outbox-published RaisedAmount credit across real
/// HTTP calls, proving the cross-module Contributions -&gt; Campaigns outbox event (verified at the
/// module-internal level by <see cref="CampaignContributionConcurrencyTests"/>) also works through
/// the full HTTP pipeline.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class ContributionAndOutboxE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public ContributionAndOutboxE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates, moderation-approves, and publishes a campaign end-to-end via HTTP, returning its id
    /// and the creator's access token. Shared setup for every test in this class that needs a
    /// Published campaign to contribute against.
    /// </summary>
    private async Task<(Guid CampaignId, string CreatorToken)> CreatePublishedCampaignAsync(HttpClient client, string label)
    {
        var creatorEmail = ApiTestExtensions.UniqueEmail($"{label}-creator");
        var (_, creatorToken) = await client.RegisterAndLoginAsync(creatorEmail, "Contribution Test Creator");
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Clean Water Wells Initiative",
            "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
            "Community",
            10_000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Contribution Test Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        // CampaignPublishedApplicationEvent must reach Contributions' replicated
        // active_campaigns_cache (TICKET-023) before a pledge against this campaign can pass
        // MakeContributionCommandHandler's local IsActive check.
        await _factory.ProcessOutboxMessagesAsync();

        return (campaignId, creatorToken);
    }

    [Fact]
    public async Task ConfirmedContribution_ShouldCreditCampaignRaisedAmount_AfterOutboxProcessing()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "credit-flow");

        var backerEmail = ApiTestExtensions.UniqueEmail("backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Backer");
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(250m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contribution = await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();
        Assert.Equal("Pending", contribution!.Status);

        var adminEmail = ApiTestExtensions.UniqueEmail("credit-flow-payments-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Payments Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contribution.ContributionId}/confirm-payment",
            new ConfirmContributionPaymentRequest("test-payment-ref-001"));
        confirmResponse.EnsureSuccessStatusCode();
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ConfirmContributionPaymentResponse>();
        Assert.Equal("Succeeded", confirmed!.Status);

        // ConfirmContributionPaymentCommandHandler only enqueues an outbox message; crediting
        // Campaign.RaisedAmount happens asynchronously via AddContributionToCampaignCommandHandler.
        await _factory.ProcessOutboxMessagesAsync();

        var campaignResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        campaignResponse.EnsureSuccessStatusCode();
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal(250m, campaign!.RaisedAmount);
        Assert.Equal("USD", campaign.RaisedCurrency);

        var contributionResponse = await client.GetAsync($"/api/campaigns/{campaignId}/contributions/{contribution.ContributionId}");
        contributionResponse.EnsureSuccessStatusCode();
        var contributionDetail = await contributionResponse.Content.ReadFromJsonAsync<GetContributionByIdResponse>();
        Assert.Equal("Succeeded", contributionDetail!.Status);
        Assert.NotNull(contributionDetail.ProcessedAtUtc);
    }

    [Fact]
    public async Task MakeContribution_WithMismatchedCurrency_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "currency-mismatch");

        var backerEmail = ApiTestExtensions.UniqueEmail("mismatched-backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Mismatched Backer");
        client.SetBearerToken(backerToken);

        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "EUR"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FailedContribution_ShouldNotCreditCampaignRaisedAmount()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "fail-flow");

        var backerEmail = ApiTestExtensions.UniqueEmail("failing-backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Failing Backer");
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(75m, "USD"));
        contributeResponse.EnsureSuccessStatusCode();
        var contribution = await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();

        var adminEmail = ApiTestExtensions.UniqueEmail("fail-flow-payments-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Payments Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        var failResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions/{contribution!.ContributionId}/fail-payment",
            new FailContributionPaymentRequest("Card declined."));
        failResponse.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxMessagesAsync();

        var campaignResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        campaignResponse.EnsureSuccessStatusCode();
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal(0m, campaign!.RaisedAmount);
    }
}
