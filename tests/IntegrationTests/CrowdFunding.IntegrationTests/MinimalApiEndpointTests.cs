using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Documentation;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Behavior specific to hosting the API as Minimal APIs next to the one comparison controller.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class MinimalApiEndpointTests
{
    private readonly CrowdFundingApiFactory _factory;

    public MinimalApiEndpointTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MalformedJson_ShouldBe400ProblemDetails_NotA500()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/identity/login", new StringContent("{ \"email\": ", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task CreatedCampaign_LocationHeader_ShouldResolveToTheCampaign()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("location-header"));
        client.SetBearerToken(token);

        var created = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Location Header Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology",
            1000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var campaignId = (await created.Content.ReadFromJsonAsync<CreateCampaignResponse>())!.CampaignId;
        Assert.EndsWith($"/api/campaigns/{campaignId}", created.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var fetched = await client.GetFromJsonAsync<GetCampaignByIdResponse>(created.Headers.Location);
        Assert.Equal(campaignId, fetched!.Id);
    }

    [Fact]
    public async Task OpenApiDocument_ShouldDescribeMinimalApisAndTheComparisonController_Alike()
    {
        using var client = _factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync($"/openapi/{OpenApiConfiguration.DocumentName}.json"));
        var paths = document.RootElement.GetProperty("paths");

        var minimalApiCreate = paths.GetProperty("/api/campaigns").GetProperty("post");
        var controllerApprove = paths.GetProperty("/api/moderation/reviews/{campaignId}/approve").GetProperty("post");

        foreach (var operation in new[] { minimalApiCreate, controllerApprove })
        {
            Assert.True(operation.TryGetProperty("security", out _));
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            Assert.True(operation.GetProperty("responses").TryGetProperty("400", out _));
        }
    }
}
