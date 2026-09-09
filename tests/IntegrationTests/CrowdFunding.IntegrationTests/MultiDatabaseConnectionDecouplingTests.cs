using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Migrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Proves TICKET-026's connection-string decoupling actually isolates a module's data at the
/// physical database level, not just in configuration: with <c>ConnectionStrings:CampaignsDb</c>
/// pointed at a second, independent Postgres instance, Campaigns writes land there while every
/// other module keeps using the shared <c>DefaultConnection</c> instance — proving a module can
/// be migrated onto its own database (Stage 2 of the evolutionary path) with zero code changes.
/// </summary>
public sealed class MultiDatabaseConnectionDecouplingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _primaryDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_primary")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly PostgreSqlContainer _campaignsDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_campaigns_isolated")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_primaryDb.StartAsync(), _campaignsDb.StartAsync(), _redis.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _primaryDb.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__CampaignsDb", _campaignsDb.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CrowdFunding.Test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CrowdFunding.Test.Client");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__Payment__PermitLimit", "100000");

        _factory = new IsolatedWebApplicationFactory();
        _ = _factory.Server;
        await MigrationRunner.RunAsync(_factory.Services);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _primaryDb.DisposeAsync();
        await _campaignsDb.DisposeAsync();
        await _redis.DisposeAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__CampaignsDb", null);
    }

    [Fact]
    public async Task CampaignsModule_ShouldWriteToItsOwnDatabase_WhileOtherModulesUseTheSharedOne()
    {
        using var client = _factory!.CreateClient();

        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("isolated-db-owner"), "Isolated DB Owner");
        client.SetBearerToken(token);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Isolated Database Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation.",
            "Technology",
            1000m,
            "USD",
            DateTime.UtcNow.AddDays(10)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();

        await using var campaignsDbConnection = new NpgsqlConnection(_campaignsDb.GetConnectionString());
        await campaignsDbConnection.OpenAsync();
        await using var campaignsCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM campaigns WHERE \"Id\" = @id", campaignsDbConnection);
        campaignsCommand.Parameters.AddWithValue("id", created!.CampaignId);
        var countInIsolatedDb = (long)(await campaignsCommand.ExecuteScalarAsync())!;
        Assert.Equal(1, countInIsolatedDb);

        await using var primaryDbConnection = new NpgsqlConnection(_primaryDb.GetConnectionString());
        await primaryDbConnection.OpenAsync();
        await using var checkTableCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'campaigns'",
            primaryDbConnection);
        var campaignsTableExistsInPrimary = (long)(await checkTableCommand.ExecuteScalarAsync())! > 0;
        Assert.False(campaignsTableExistsInPrimary);

        await using var identityCheckCommand = new NpgsqlCommand("SELECT COUNT(*) FROM users", primaryDbConnection);
        var usersInPrimary = (long)(await identityCheckCommand.ExecuteScalarAsync())!;
        Assert.True(usersInPrimary > 0);
    }

    private sealed class IsolatedWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
