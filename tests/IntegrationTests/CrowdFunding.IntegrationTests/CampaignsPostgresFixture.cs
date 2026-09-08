using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Spins up a real PostgreSQL 16 container and applies the Campaigns module migrations,
/// so concurrency behavior (xmin optimistic locking, pg_advisory_xact_lock, the
/// campaign_contributions_ledger unique constraint) can be verified against actual Postgres
/// semantics rather than the EF Core in-memory provider, which does not implement any of them.
/// </summary>
public sealed class CampaignsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_integration_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new CampaignsDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public CampaignsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CampaignsDbContext(options);
    }
}
