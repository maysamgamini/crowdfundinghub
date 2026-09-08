using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Real-Postgres fixture for the Contributions module, mirroring CampaignsPostgresFixture —
/// needed to verify optimistic concurrency (xmin) behavior that the EF Core in-memory provider
/// does not implement.
/// </summary>
public sealed class ContributionsPostgresFixture : IAsyncLifetime
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

        var options = new DbContextOptionsBuilder<ContributionsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new ContributionsDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new <see cref="ContributionsDbContext"/> connected to the containerized database.
    /// </summary>
    /// <returns>A configured <see cref="ContributionsDbContext"/> instance.</returns>
    public ContributionsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ContributionsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ContributionsDbContext(options);
    }
}

/// <summary>
/// xUnit collection definition fixture for shared Contributions PostgreSQL container instances.
/// </summary>
[CollectionDefinition(nameof(ContributionsPostgresCollection))]
public sealed class ContributionsPostgresCollection : ICollectionFixture<ContributionsPostgresFixture>;
