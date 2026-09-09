using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Migrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-023's core claim: once a campaign is replicated into Contributions' own
/// <c>active_campaigns_cache</c>, pledging against it never needs Campaigns' database again.
/// Proven here by pointing Campaigns at its own isolated Postgres container, publishing a
/// campaign (which replicates it into Contributions — on the shared/default connection), then
/// killing the Campaigns container outright and confirming a pledge still succeeds.
/// </summary>
public sealed class ReplicatedReadModelResilienceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _sharedDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_shared")
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

    private IsolatedWebApplicationFactory? _factory;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sharedDb.StartAsync(), _campaignsDb.StartAsync(), _redis.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _sharedDb.GetConnectionString());
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

        await _sharedDb.DisposeAsync();
        await _redis.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__CampaignsDb", null);

        // Already stopped mid-test below; disposing an already-stopped container is a no-op.
        await _campaignsDb.DisposeAsync();
    }

    [Fact]
    public async Task MakeContribution_ShouldSucceed_AfterCampaignsDatabaseIsCompletelyUnreachable()
    {
        using var client = _factory!.CreateClient();

        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("resilience-creator"), "Resilience Creator");
        client.SetBearerToken(creatorToken);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Resilience Proof Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology",
            5000m,
            "USD",
            DateTime.UtcNow.AddDays(30)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;

        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("resilience-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Resilience Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/moderation/reviews/{campaignId}/approve",
            new CrowdFunding.API.Contracts.Moderation.ReviewCampaignRequest(Notes: null));
        approveResponse.EnsureSuccessStatusCode();

        client.SetBearerToken(creatorToken);
        var publishResponse = await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null);
        publishResponse.EnsureSuccessStatusCode();

        // Replicates CampaignPublishedApplicationEvent into Contributions' own database (on the
        // shared connection) while Campaigns' own database is still up.
        await _factory.ProcessOutboxMessagesAsync();

        // Now sever Campaigns' database entirely — not paused, not slow, gone.
        await _campaignsDb.StopAsync();

        var backerEmail = ApiTestExtensions.UniqueEmail("resilience-backer");
        var (_, backerToken) = await client.RegisterAndLoginAsync(backerEmail, "Resilience Backer");
        client.SetBearerToken(backerToken);

        var contributeResponse = await client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD"));

        contributeResponse.EnsureSuccessStatusCode();
        var contribution = await contributeResponse.Content.ReadFromJsonAsync<MakeContributionResponse>();
        Assert.Equal("Pending", contribution!.Status);
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

        public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = Services.CreateScope();
            var dispatchers = scope.ServiceProvider
                .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                .OfType<CrowdFunding.BuildingBlocks.Infrastructure.Outbox.IOutboxDispatcher>();

            foreach (var dispatcher in dispatchers)
            {
                await dispatcher.ProcessBatchAsync(cancellationToken);
            }
        }
    }
}
