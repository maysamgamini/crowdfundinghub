using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Common;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Reading a pledge reveals who backed what and the payment references, so it needs a token and
/// the right to see that pledge. These tests drive real HTTP calls with no token, with another
/// backer's token, with the campaign owner's token and with a payments manager's token.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PledgeReadAuthorizationE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public PledgeReadAuthorizationE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record Scenario(Guid CampaignId, Guid PledgeId, string OwnerToken, string BackerToken, string OtherBackerToken, string AdminToken);

    private async Task<Scenario> ArrangeAsync(string label)
    {
        using var client = _factory.CreateClient();
        var (_, ownerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-owner"), "Owner");
        client.SetBearerToken(ownerToken);
        var created = await (await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Read Authorization Campaign",
            "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
            "Community", 10_000m, "USD", DateTime.UtcNow.AddDays(30)))).Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;
        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        (await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null))).EnsureSuccessStatusCode();

        client.SetBearerToken(ownerToken);
        (await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-backer"), "Backer");
        var (_, otherToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-other"), "Other");
        client.SetBearerToken(backerToken);
        var pledge = await (await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/pledges", new MakePledgeRequest(100m, "USD")))
            .Content.ReadFromJsonAsync<MakePledgeResponse>();

        return new Scenario(campaignId, pledge!.PledgeId, ownerToken, backerToken, otherToken, adminToken);
    }

    private HttpClient As(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.SetBearerToken(token);
        }

        return client;
    }

    [Fact]
    public async Task WithoutAToken_BothReadEndpointsAnswer401()
    {
        var s = await ArrangeAsync("anon");
        using var anonymous = As(null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/campaigns/{s.CampaignId}/pledges")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/campaigns/{s.CampaignId}/pledges/{s.PledgeId}")).StatusCode);
    }

    [Fact]
    public async Task TheBackerWhoPledged_CanReadThePledgeInFull()
    {
        var s = await ArrangeAsync("own");
        using var backer = As(s.BackerToken);

        var response = await backer.GetAsync($"/api/campaigns/{s.CampaignId}/pledges/{s.PledgeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetPledgeByIdResponse>();
        Assert.False(string.IsNullOrEmpty(body!.ExternalPaymentIntentId));
    }

    [Fact]
    public async Task AnotherBacker_GetsNotFoundForThePledge_AndAnEmptyList()
    {
        var s = await ArrangeAsync("other");
        using var other = As(s.OtherBackerToken);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/campaigns/{s.CampaignId}/pledges/{s.PledgeId}")).StatusCode);

        var list = await other.GetFromJsonAsync<PagedResponse<ListPledgesResponse>>($"/api/campaigns/{s.CampaignId}/pledges");
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task TheCampaignOwner_SeesThePledgeWithoutItsPaymentIntent()
    {
        var s = await ArrangeAsync("owner");
        using var owner = As(s.OwnerToken);

        var pledge = await owner.GetFromJsonAsync<GetPledgeByIdResponse>($"/api/campaigns/{s.CampaignId}/pledges/{s.PledgeId}");
        var list = await owner.GetFromJsonAsync<PagedResponse<ListPledgesResponse>>($"/api/campaigns/{s.CampaignId}/pledges");

        Assert.Null(pledge!.ExternalPaymentIntentId);
        Assert.Single(list!.Items);
    }

    [Fact]
    public async Task APaymentsManager_SeesThePledgeInFull()
    {
        var s = await ArrangeAsync("manager");
        using var admin = As(s.AdminToken);

        var pledge = await admin.GetFromJsonAsync<GetPledgeByIdResponse>($"/api/campaigns/{s.CampaignId}/pledges/{s.PledgeId}");

        Assert.False(string.IsNullOrEmpty(pledge!.ExternalPaymentIntentId));
    }
}
