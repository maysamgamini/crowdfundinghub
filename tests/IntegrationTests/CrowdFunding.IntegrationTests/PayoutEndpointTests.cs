using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// GET /api/campaigns/{id}/payout: the creator sees the amounts and the ledger behind the balance; a stranger and a
/// campaign without a payout get the same "not found", and a caller who is not signed in is turned away.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PayoutEndpointTests
{
    private readonly CrowdFundingApiFactory _factory;

    public PayoutEndpointTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedPaidPayoutAsync(Guid ownerId)
    {
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var pledge = Pledge.Create(campaignId, Guid.NewGuid(), 200m, "USD", now);
        pledge.ConfirmPayment("endpoint-ref", now);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.Pledges.Add(pledge);
        db.Payouts.Add(Payout.Create(campaignId, ownerId, "USD", now));
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<PayoutService>().PayDueAsync(50, CancellationToken.None);
        return campaignId;
    }

    [Fact]
    public async Task TheCreator_SeesTheAmountsAndTheLedger_OfTheirPayout()
    {
        using var client = _factory.CreateClient();
        var (ownerId, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("payout-owner"), "Payout Owner");
        var campaignId = await SeedPaidPayoutAsync(ownerId);

        client.SetBearerToken(token);
        var response = await client.GetAsync($"/api/campaigns/{campaignId}/payout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payout = (await response.Content.ReadFromJsonAsync<GetCampaignPayoutResponse>())!;
        Assert.Equal("Paid", payout.Status);
        Assert.Equal(200m, payout.GrossAmount);
        Assert.Equal(10m, payout.FeeAmount);
        Assert.Equal(190m, payout.NetAmount);
        Assert.Equal(0m, payout.Balance);
        Assert.Contains(payout.Lines, x => x.Kind == "PaidOut" && x.Amount == -190m);
    }

    [Fact]
    public async Task AStranger_GetsNotFound_AndSoDoesACampaignWithoutAPayout()
    {
        using var client = _factory.CreateClient();
        var (ownerId, _) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("payout-owner2"), "Payout Owner");
        var campaignId = await SeedPaidPayoutAsync(ownerId);
        var (_, strangerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("payout-stranger"), "Stranger");

        client.SetBearerToken(strangerToken);
        var asStranger = await client.GetAsync($"/api/campaigns/{campaignId}/payout");
        var noPayout = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}/payout");

        Assert.Equal(HttpStatusCode.NotFound, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noPayout.StatusCode);
    }

    [Fact]
    public async Task APaymentsManager_CanReadAnyPayout()
    {
        using var client = _factory.CreateClient();
        var campaignId = await SeedPaidPayoutAsync(Guid.NewGuid());
        var adminEmail = ApiTestExtensions.UniqueEmail("payout-ledger-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");

        client.SetBearerToken(await client.LoginUserAsync(adminEmail));
        var response = await client.GetAsync($"/api/campaigns/{campaignId}/payout");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ACallerWhoIsNotSignedIn_IsTurnedAway()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/campaigns/{Guid.NewGuid()}/payout");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
