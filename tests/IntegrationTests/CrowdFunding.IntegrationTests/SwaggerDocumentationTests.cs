using CrowdFunding.API.Controllers;
using CrowdFunding.API.Documentation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Verifies the correctness, schema generation, security definitions, and XML comments in OpenAPI/Swagger documentation.
/// </summary>
public sealed class SwaggerDocumentationTests
{
    private readonly OpenApiDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaggerDocumentationTests"/> class, building test services and generating Swagger docs.
    /// </summary>
    public SwaggerDocumentationTests()
    {
        var services = new ServiceCollection();

        var mockEnvironment = new MockWebHostEnvironment();
        services.AddSingleton<IWebHostEnvironment>(mockEnvironment);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(mockEnvironment);

        services.AddLogging();
        var mvcBuilder = services.AddControllers();
        mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(IdentityController).Assembly));
        services.AddEndpointsApiExplorer();
        services.AddCrowdFundingSwagger();

        var serviceProvider = services.BuildServiceProvider();
        var swaggerProvider = serviceProvider.GetRequiredService<ISwaggerProvider>();
        _document = swaggerProvider.GetSwagger(SwaggerConfiguration.ApiVersion);
    }

    [Fact]
    public void SwaggerDocument_ShouldHaveCorrectApiInfo()
    {
        Assert.NotNull(_document);
        Assert.Equal("CrowdFunding Hub API", _document.Info.Title);
        Assert.Equal("v1", _document.Info.Version);
        Assert.Contains("Modular Monolith", _document.Info.Description);
        Assert.Contains("ES256", _document.Info.Description);
        Assert.Contains("RFC 9457", _document.Info.Description);
    }

    [Fact]
    public void SwaggerDocument_ShouldConfigureJwtBearerSecurity()
    {
        Assert.NotNull(_document.Components);
        var components = _document.Components!;
        Assert.NotNull(components.SecuritySchemes);
        Assert.True(components.SecuritySchemes.ContainsKey(SwaggerConfiguration.SecuritySchemeName));

        var scheme = components.SecuritySchemes[SwaggerConfiguration.SecuritySchemeName];
        Assert.NotNull(scheme);
        Assert.Equal(SecuritySchemeType.Http, scheme.Type);
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);

        Assert.NotNull(_document.Security);
        Assert.NotEmpty(_document.Security);
        var requirement = _document.Security.First();
        Assert.NotEmpty(requirement.Keys);
    }

    [Fact]
    public void SwaggerDocument_ShouldContainModuleTagsWithDescriptions()
    {
        var expectedTags = new[] { "Identity", "Campaigns", "Contributions", "Moderation", "System" };

        Assert.NotNull(_document.Tags);
        foreach (var expectedTag in expectedTags)
        {
            var tag = _document.Tags!.FirstOrDefault(t => string.Equals(t.Name, expectedTag, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(tag);
            Assert.False(string.IsNullOrWhiteSpace(tag!.Description), $"Tag '{expectedTag}' should have a description.");
        }
    }

    [Fact]
    public void SwaggerDocument_ShouldContainAllControllerEndpoints()
    {
        Assert.NotNull(_document.Paths);
        var paths = _document.Paths!;

        Assert.True(paths.ContainsKey("/api/Identity/register"));
        Assert.True(paths.ContainsKey("/api/Identity/login"));
        Assert.True(paths.ContainsKey("/api/Identity/me"));
        Assert.True(paths.ContainsKey("/api/Identity/users/{userId}/roles"));
        Assert.True(paths.ContainsKey("/api/Identity/users/{userId}/permissions"));

        Assert.True(paths.ContainsKey("/api/Campaigns"));
        Assert.True(paths.ContainsKey("/api/Campaigns/{id}"));
        Assert.True(paths.ContainsKey("/api/Campaigns/{id}/publish"));
        Assert.True(paths.ContainsKey("/api/Campaigns/{id}/cancel"));

        Assert.True(paths.ContainsKey("/api/campaigns/{campaignId}/Contributions"));
        Assert.True(paths.ContainsKey("/api/campaigns/{campaignId}/Contributions/{contributionId}/confirm-payment"));
        Assert.True(paths.ContainsKey("/api/campaigns/{campaignId}/Contributions/{contributionId}/fail-payment"));

        Assert.True(paths.ContainsKey("/api/moderation/campaigns/{campaignId}"));
        Assert.True(paths.ContainsKey("/api/moderation/campaigns/{campaignId}/approve"));
        Assert.True(paths.ContainsKey("/api/moderation/campaigns/{campaignId}/reject"));
    }

    [Fact]
    public void SwaggerDocument_ShouldIncludeXmlCommentsForEndpoints()
    {
        Assert.NotNull(_document.Paths);
        var paths = _document.Paths!;

        var registerPath = paths["/api/Identity/register"];
        Assert.NotNull(registerPath);
        Assert.NotNull(registerPath.Operations);
        var registerPost = registerPath.Operations[HttpMethod.Post];
        Assert.NotNull(registerPost);
        Assert.False(string.IsNullOrWhiteSpace(registerPost.Summary));
        Assert.Contains("Register", registerPost.Summary, StringComparison.OrdinalIgnoreCase);

        var createCampaignPath = paths["/api/Campaigns"];
        Assert.NotNull(createCampaignPath);
        Assert.NotNull(createCampaignPath.Operations);
        var createCampaignPost = createCampaignPath.Operations[HttpMethod.Post];
        Assert.NotNull(createCampaignPost);
        Assert.False(string.IsNullOrWhiteSpace(createCampaignPost.Summary));
        Assert.Contains("campaign", createCampaignPost.Summary, StringComparison.OrdinalIgnoreCase);

        var makeContributionPath = paths["/api/campaigns/{campaignId}/Contributions"];
        Assert.NotNull(makeContributionPath);
        Assert.NotNull(makeContributionPath.Operations);
        var makeContributionPost = makeContributionPath.Operations[HttpMethod.Post];
        Assert.NotNull(makeContributionPost);
        Assert.False(string.IsNullOrWhiteSpace(makeContributionPost.Summary));
    }

    [Fact]
    public void SwaggerDocument_ShouldIncludeXmlCommentsForSchemas()
    {
        Assert.NotNull(_document.Components);
        var components = _document.Components!;
        Assert.NotNull(components.Schemas);

        Assert.True(components.Schemas.ContainsKey("RegisterUserRequest") ||
                    components.Schemas.ContainsKey("CreateCampaignRequest") ||
                    components.Schemas.ContainsKey("MakeContributionRequest"));
    }

    private sealed class MockWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "CrowdFunding.API";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
    }
}
