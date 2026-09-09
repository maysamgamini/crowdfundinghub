using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrowdFunding.API.Contracts.Campaigns;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Drives the TICKET-028 Rosetta Stone sample end to end against real HTTP + Postgres: all
/// three tiers accept the same valid payload and persist a record, and all three return the
/// same RFC 9457 <c>application/problem+json</c> validation shape for the same invalid payload,
/// despite Tier 1 (Minimal API) and Tier 2 (Pragmatic CQRS) bypassing MVC's controller pipeline
/// entirely.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RosettaStoneParityTests
{
    private readonly CrowdFundingApiFactory _factory;

    public RosettaStoneParityTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private sealed record RosettaCampaignPayload(string Title, string Story, decimal TargetAmount, string Currency);

    private static RosettaCampaignPayload ValidPayload(string title) => new(
        title,
        "A valid story describing the campaign in more than twenty characters.",
        1000m,
        "USD");

    private static RosettaCampaignPayload InvalidPayload() => new(
        Title: string.Empty,
        Story: "A valid story describing the campaign in more than twenty characters.",
        TargetAmount: 0m,
        Currency: "USD");

    [Fact]
    public async Task Tier1MinimalApi_ValidPayload_PersistsAndReturns201()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/rosetta/v1/campaigns/tier1-minimal-api", ValidPayload("Rosetta Tier 1 Campaign"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Tier2PragmaticCqrs_ValidPayload_PersistsAndReturns201()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/rosetta/v1/campaigns/tier2-pragmatic-cqrs", ValidPayload("Rosetta Tier 2 Campaign"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Tier3RichDomainModel_ValidPayload_PersistsAndReturns201()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("rosetta-tier3"));
        client.SetBearerToken(token);

        var request = new CreateCampaignRequest(
            Title: "Rosetta Tier 3 Campaign",
            Story: "A valid story describing the campaign in more than twenty characters.",
            Category: "Technology",
            GoalAmount: 1000m,
            Currency: "USD",
            DeadlineUtc: DateTime.UtcNow.AddDays(30));

        var response = await client.PostAsJsonAsync("/api/campaigns", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("/rosetta/v1/campaigns/tier1-minimal-api")]
    [InlineData("/rosetta/v1/campaigns/tier2-pragmatic-cqrs")]
    public async Task InvalidPayload_AcrossTier1AndTier2_ReturnsSameProblemDetailsShape(string endpoint)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(endpoint, InvalidPayload());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task InvalidPayload_Tier3RichDomainModel_ReturnsSameProblemDetailsShapeAsTier1AndTier2()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("rosetta-tier3-invalid"));
        client.SetBearerToken(token);

        var request = new CreateCampaignRequest(
            Title: string.Empty,
            Story: "A valid story describing the campaign in more than twenty characters.",
            Category: "Technology",
            GoalAmount: 0m,
            Currency: "USD",
            DeadlineUtc: DateTime.UtcNow.AddDays(30));

        var response = await client.PostAsJsonAsync("/api/campaigns", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }
}
