using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Drives Make Pledge -&gt; Confirm Payment -&gt; outbox-published RaisedAmount credit across real
/// HTTP calls, proving the cross-module Pledges -&gt; Campaigns outbox event (verified at the
/// module-internal level by <see cref="CampaignPledgeConcurrencyTests"/>) also works through
/// the full HTTP pipeline.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PledgeAndOutboxE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public PledgeAndOutboxE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates, moderation-approves, and publishes a campaign end-to-end via HTTP, returning its id
    /// and the creator's access token. Shared setup for every test in this class that needs a
    /// Published campaign to pledge against.
    /// </summary>
    private async Task<(Guid CampaignId, string CreatorToken)> CreatePublishedCampaignAsync(HttpClient client, string label)
    {
        var creatorEmail = ApiTestExtensions.UniqueEmail($"{label}-creator");
        var (_, creatorToken) = await client.RegisterAndLoginAsync(creatorEmail, "Pledge Test Creator");
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
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Pledge Test Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);

        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        // CampaignPublishedApplicationEvent must reach Pledges' replicated
        // active_campaigns_cache (TICKET-023) before a pledge against this campaign can pass
        // MakePledgeCommandHandler's local IsActive check.
        await _factory.ProcessOutboxMessagesAsync();

        return (campaignId, creatorToken);
    }

    [Fact]
    public async Task ConfirmedPledge_ShouldCreditCampaignRaisedAmount_AfterOutboxProcessing()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "credit-flow");

        var backerEmail = ApiTestExtensions.UniqueEmail("backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Backer");
        client.SetBearerToken(backerToken);

        var makeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(250m, "USD"));
        makeResponse.EnsureSuccessStatusCode();
        var pledge = await makeResponse.Content.ReadFromJsonAsync<MakePledgeResponse>();
        Assert.Equal("Pending", pledge!.Status);

        var adminEmail = ApiTestExtensions.UniqueEmail("credit-flow-payments-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Payments Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        var confirmResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges/{pledge.PledgeId}/confirm-payment",
            new ConfirmPledgePaymentRequest("test-payment-ref-001"));
        confirmResponse.EnsureSuccessStatusCode();
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<ConfirmPledgePaymentResponse>();
        Assert.Equal("Succeeded", confirmed!.Status);

        // ConfirmPledgePaymentCommandHandler only enqueues an outbox message; crediting
        // Campaign.RaisedAmount happens asynchronously via AddPledgeToCampaignCommandHandler.
        await _factory.ProcessOutboxMessagesAsync();

        var campaignResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        campaignResponse.EnsureSuccessStatusCode();
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal(250m, campaign!.RaisedAmount);
        Assert.Equal("USD", campaign.RaisedCurrency);

        var pledgeResponse = await client.GetAsync($"/api/campaigns/{campaignId}/pledges/{pledge.PledgeId}");
        pledgeResponse.EnsureSuccessStatusCode();
        var pledgeDetail = await pledgeResponse.Content.ReadFromJsonAsync<GetPledgeByIdResponse>();
        Assert.Equal("Succeeded", pledgeDetail!.Status);
        Assert.NotNull(pledgeDetail.ProcessedAtUtc);
    }

    [Fact]
    public async Task MakePledge_WithMismatchedCurrency_ShouldReturnBadRequest()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "currency-mismatch");

        var backerEmail = ApiTestExtensions.UniqueEmail("mismatched-backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Mismatched Backer");
        client.SetBearerToken(backerToken);

        var response = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(100m, "EUR"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FailedPledge_ShouldNotCreditCampaignRaisedAmount()
    {
        using var client = _factory.CreateClient();
        var (campaignId, _) = await CreatePublishedCampaignAsync(client, "fail-flow");

        var backerEmail = ApiTestExtensions.UniqueEmail("failing-backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Failing Backer");
        client.SetBearerToken(backerToken);

        var makeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(75m, "USD"));
        makeResponse.EnsureSuccessStatusCode();
        var pledge = await makeResponse.Content.ReadFromJsonAsync<MakePledgeResponse>();

        var adminEmail = ApiTestExtensions.UniqueEmail("fail-flow-payments-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Payments Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);

        var failResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/pledges/{pledge!.PledgeId}/fail-payment",
            new FailPledgePaymentRequest("Card declined."));
        failResponse.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxMessagesAsync();

        var campaignResponse = await client.GetAsync($"/api/campaigns/{campaignId}");
        campaignResponse.EnsureSuccessStatusCode();
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<GetCampaignByIdResponse>();
        Assert.Equal(0m, campaign!.RaisedAmount);
    }
}
