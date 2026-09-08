using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CrowdFunding.API.Observability;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Exercises cross-cutting API platform pieces that the module-focused E2E suites never happen
/// to hit: the JWKS discovery endpoint, correlation id propagation, the exact RFC 9457
/// <c>application/problem+json</c> shape written by <see cref="GlobalExceptionHandler"/>, and
/// that the rate limiter actually rejects requests once its permit budget is exhausted.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class ApiPlatformE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public ApiPlatformE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Jwks_ShouldReturnPublicEs256KeySet()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = document.RootElement.GetProperty("keys");

        Assert.True(keys.GetArrayLength() > 0);
        var key = keys[0];
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("P-256", key.GetProperty("crv").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("kid").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("x").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(key.GetProperty("y").GetString()));

        // The endpoint builds its payload manually from ECDsa.ExportParameters(includePrivateParameters: false)
        // rather than a generic JWK converter, specifically so no private component can ever be
        // serialized — assert that invariant directly.
        Assert.False(key.TryGetProperty("d", out _));
    }

    [Fact]
    public async Task EveryResponse_ShouldCarryACorrelationIdHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.CorrelationHeader));
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.CorrelationHeader).Single();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task NotFoundResponse_ShouldBeShapedAsRfc9457ProblemDetails()
    {
        using var client = _factory.CreateClient();
        var campaignId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/campaigns/{campaignId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", root.GetProperty("title").GetString());
        Assert.Contains(campaignId.ToString(), root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Equal($"/api/campaigns/{campaignId}", root.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task AuthPolicy_ShouldReject429_OnceLoginPermitsAreExhausted()
    {
        // Deliberately scoped to this one test via WithWebHostBuilder: the shared factory raises
        // rate limits sky-high (see CrowdFundingApiFactory) so every other E2E test isn't flaky
        // against a shared partition key (TestServer requests all share one synthetic client IP).
        // Overriding just the auth policy's permit limit down to 1 here proves the limiter itself
        // still works, without reintroducing that flakiness everywhere else.
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "1");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__WindowSeconds", "60");

        await using var factory = _factory.WithWebHostBuilder(_ => { });
        using var client = factory.CreateClient();

        var email = ApiTestExtensions.UniqueEmail("rate-limited");
        var first = await client.PostAsJsonAsync(
            "/api/identity/login", new { Email = email, Password = "whatever" });
        var second = await client.PostAsJsonAsync(
            "/api/identity/login", new { Email = email, Password = "whatever" });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__WindowSeconds", null);
    }
}
