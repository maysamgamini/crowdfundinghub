using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Boots the full ASP.NET Core pipeline (routing, model binding/validation, JWT Bearer
/// authentication, authorization policies, rate limiting, <see cref="Program"/>'s
/// <c>GlobalExceptionHandler</c>) against real PostgreSQL and Redis Testcontainers, so HTTP-level
/// behavior can be exercised end-to-end instead of only the module-internal concurrency tests in
/// <see cref="CampaignsPostgresFixture"/>/<see cref="ContributionsPostgresFixture"/>.
///
/// All four module DbContexts share one Postgres database (each with its own
/// <c>__EFMigrationsHistory</c> schema — see the module DependencyInjection classes), so a single
/// container suffices.
/// </summary>
public sealed class CrowdFundingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_e2e_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();

        // Program.cs reads Jwt:*/ConnectionStrings:*/OpenMeter:* eagerly via `builder.Configuration`
        // in its own top-level statements, executed while WebApplicationFactory is still replaying
        // Program.Main — before its ConfigureAppConfiguration callback (see ConfigureWebHost below)
        // has been merged in. Process environment variables are read directly into
        // WebApplicationBuilder's own configuration during WebApplication.CreateBuilder(args), so
        // they're the only override guaranteed visible to those early reads; ConfigureAppConfiguration
        // is kept too for the (later-resolved, DI-based) config reads elsewhere in the pipeline.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _dbContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redisContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CrowdFunding.Test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CrowdFunding.Test.Client");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("OpenMeter__BaseUrl", "https://openmeter.cloud");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__Payment__PermitLimit", "100000");

        // TICKET-050's dispatch-time SSRF guard correctly refuses to connect to loopback/private
        // addresses, which is exactly what WebhookSubscriptionE2ETests.
        // DispatchingADueDelivery_ShouldPostASignedRequest_ThatARealReceiverCanVerify needs to
        // reach its in-process stand-in receiver on 127.0.0.1. This is test-host-only — see
        // SsrfSafeHttpMessageHandlerFactory's doc comment for why nothing outside this fixture
        // should ever set it.
        Environment.SetEnvironmentVariable("WebhookDispatcher__AllowPrivateNetworkTargets", "true");

        // Force the host to build (WebApplicationFactory builds it lazily on first access to
        // Server/Services), then apply every module's migrations up front so individual tests
        // never race the schema.
        _ = Server;
        await MigrationRunner.RunAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Overrides the content root that <c>Microsoft.AspNetCore.Mvc.Testing</c> auto-discovers from
    /// its generated <c>MvcTestingAppManifest.json</c>. That manifest embeds each project
    /// reference's directory as a literal, unevaluated MSBuild property-function expression
    /// (<c>$([System.IO.Path]::GetDirectoryName('...'))</c>) — evaluating it fails whenever the
    /// repository path itself contains a single quote (as this one's parent directory does), and
    /// the broken text is then fed straight to <see cref="Microsoft.Extensions.FileProviders.PhysicalFileProvider"/>
    /// as a literal path. Adding our own <c>UseContentRoot</c> call here — the last configuration
    /// step before <see cref="WebApplicationFactory{TEntryPoint}.CreateHost"/> builds the host —
    /// wins over that broken value once the host's configuration is composed.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" instead of "Development" — skips Program's own IsDevelopment() auto-migrate
        // and Swagger UI registration, since this fixture drives migrations explicitly above.
        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Runs one outbox claim-and-publish pass on every module's autonomous outbox worker
    /// (<see cref="IOutboxDispatcher"/>) synchronously, so tests can deterministically observe
    /// cross-module side effects (e.g. Campaign Created -> Moderation Review Created) instead of
    /// waiting on each worker's 5-second polling interval. Discovers dispatchers by interface
    /// rather than by module DbContext type, so this fixture needs no changes when a module is
    /// added, removed, or extracted into its own service.
    /// </summary>
    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dispatchers = scope.ServiceProvider
            .GetServices<IHostedService>()
            .OfType<IOutboxDispatcher>();

        foreach (var dispatcher in dispatchers)
        {
            await dispatcher.ProcessBatchAsync(cancellationToken);
        }
    }
}
