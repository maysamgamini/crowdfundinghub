using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CrowdFunding.UnitTests;

public sealed class ConfigurationExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void GetRequiredModuleConnectionString_ShouldPreferModuleSpecificKey_WhenPresent()
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:CampaignsDb"] = "Host=campaigns-cluster;Database=campaigns",
            ["ConnectionStrings:DefaultConnection"] = "Host=shared;Database=crowdfunding",
        });

        var connectionString = configuration.GetRequiredModuleConnectionString("CampaignsDb");

        Assert.Equal("Host=campaigns-cluster;Database=campaigns", connectionString);
    }

    [Fact]
    public void GetRequiredModuleConnectionString_ShouldFallBackToDefaultConnection_WhenModuleKeyIsAbsent()
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=shared;Database=crowdfunding",
        });

        var connectionString = configuration.GetRequiredModuleConnectionString("CampaignsDb");

        Assert.Equal("Host=shared;Database=crowdfunding", connectionString);
    }

    [Fact]
    public void GetRequiredModuleConnectionString_ShouldThrow_WithBothKeyNamesInMessage_WhenNeitherIsConfigured()
    {
        var configuration = BuildConfiguration([]);

        var action = () => configuration.GetRequiredModuleConnectionString("CampaignsDb");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("CampaignsDb", exception.Message);
        Assert.Contains("DefaultConnection", exception.Message);
    }

    [Fact]
    public void GetRequiredModuleConnectionString_ShouldUseCustomFallbackKey_WhenSpecified()
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:SharedFallback"] = "Host=shared;Database=crowdfunding",
        });

        var connectionString = configuration.GetRequiredModuleConnectionString("CampaignsDb", "SharedFallback");

        Assert.Equal("Host=shared;Database=crowdfunding", connectionString);
    }
}
