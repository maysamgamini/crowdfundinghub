using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Real-Postgres fixture for the Pledges module, mirroring CampaignsPostgresFixture —
/// needed to verify optimistic concurrency (xmin) behavior that the EF Core in-memory provider
/// does not implement.
/// </summary>
public sealed class PledgesPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("crowdfunding_integration_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>
    /// The connection string to the running PostgreSQL container.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<PledgesDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new PledgesDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new <see cref="PledgesDbContext"/> connected to the containerized database.
    /// </summary>
    /// <returns>A configured <see cref="PledgesDbContext"/> instance.</returns>
    public PledgesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PledgesDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new PledgesDbContext(options);
    }
}

/// <summary>
/// xUnit collection definition fixture for shared Pledges PostgreSQL container instances.
/// </summary>
[CollectionDefinition(nameof(PledgesPostgresCollection))]
public sealed class PledgesPostgresCollection : ICollectionFixture<PledgesPostgresFixture>;
