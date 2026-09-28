using System.Net;
using System.Text.Json;
using CrowdFunding.API.Documentation;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The OpenAPI document the running API serves (built-in Microsoft.AspNetCore.OpenApi): metadata,
/// tags, the bearer scheme applied only where authentication is required, XML-comment summaries,
/// and the Scalar UI.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OpenApiDocumentationTests
{
    private readonly CrowdFundingApiFactory _factory;

    public OpenApiDocumentationTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Document_ShouldDescribeTheApi()
    {
        using var document = await GetDocumentAsync();
        var info = document.RootElement.GetProperty("info");

        Assert.StartsWith("3.", document.RootElement.GetProperty("openapi").GetString());
        Assert.Equal(OpenApiConfiguration.ApiTitle, info.GetProperty("title").GetString());
        Assert.Contains("RFC 9457", info.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Document_ShouldDeclareTheBearerScheme_OnlyOnAuthenticatedOperations()
    {
        using var document = await GetDocumentAsync();
        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty(OpenApiConfiguration.SecuritySchemeName);
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());

        var paths = document.RootElement.GetProperty("paths");
        Assert.True(FindOperation(paths, "/login", "post").TryGetProperty("security", out _) is false);
        Assert.True(FindOperation(paths, "/me", "get").TryGetProperty("security", out var security));
        Assert.True(security.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Document_ShouldCarryTagDescriptions_AndXmlCommentSummaries()
    {
        using var document = await GetDocumentAsync();

        var tags = document.RootElement.GetProperty("tags").EnumerateArray().ToDictionary(
            tag => tag.GetProperty("name").GetString()!,
            tag => tag.GetProperty("description").GetString());
        foreach (var name in new[] { "Identity", "Campaigns", "Contributions", "Moderation", "System" })
        {
            Assert.False(string.IsNullOrWhiteSpace(tags[name]));
        }

        var register = FindOperation(document.RootElement.GetProperty("paths"), "/register", "post");
        Assert.Contains("Register", register.GetProperty("summary").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScalarUi_ShouldBeServed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/scalar/{OpenApiConfiguration.DocumentName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/openapi/{OpenApiConfiguration.DocumentName}.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement FindOperation(JsonElement paths, string pathSuffix, string method)
    {
        var path = paths.EnumerateObject().Single(p => p.Name.EndsWith(pathSuffix, StringComparison.OrdinalIgnoreCase)
                                                        && p.Name.Contains("identity", StringComparison.OrdinalIgnoreCase));
        return path.Value.GetProperty(method);
    }
}
