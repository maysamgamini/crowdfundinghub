using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// Real-database checks of the module data boundary after every module's migrations have run.
/// These tests only inspect the PostgreSQL catalog. They do not prove that no code queries
/// another module's tables: the modules still share one database and one login.
/// </summary>
public sealed class ModuleSchemaRulesTests : IAsyncLifetime
{
    private static readonly string[] ModuleSchemas =
        ["campaigns", "pledges", "identity", "moderation", "notifications", "campaignupdates"];

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("module_schema_rules")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var cs = _container.GetConnectionString();

        await Migrate(new CampaignsDbContext(Options<CampaignsDbContext>(cs, "campaigns")));
        await Migrate(new PledgesDbContext(Options<PledgesDbContext>(cs, "pledges")));
        await Migrate(new IdentityDbContext(Options<IdentityDbContext>(cs, "identity")));
        await Migrate(new ModerationDbContext(Options<ModerationDbContext>(cs, "moderation")));
        await Migrate(new NotificationsDbContext(Options<NotificationsDbContext>(cs, "notifications")));
        await Migrate(new CampaignUpdatesDbContext(Options<CampaignUpdatesDbContext>(cs, "campaignupdates")));
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task EveryModuleTable_LivesInItsModuleSchema_NotInPublic()
    {
        var publicTables = await QueryStrings(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");

        Assert.Empty(publicTables);
    }

    [Fact]
    public async Task EveryModuleSchema_ContainsTablesAndItsOwnMigrationHistory()
    {
        foreach (var schema in ModuleSchemas)
        {
            var tables = await QueryStrings(
                $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{schema}'");

            Assert.Contains("__EFMigrationsHistory", tables);
            Assert.True(tables.Count > 1, $"Schema '{schema}' has no module tables.");
        }
    }

    [Fact]
    public async Task NoForeignKey_CrossesAModuleSchemaBoundary()
    {
        var crossing = await QueryStrings(
            """
            SELECT n1.nspname || '.' || c1.relname || ' -> ' || n2.nspname || '.' || c2.relname
            FROM pg_constraint k
            JOIN pg_class c1 ON c1.oid = k.conrelid
            JOIN pg_namespace n1 ON n1.oid = c1.relnamespace
            JOIN pg_class c2 ON c2.oid = k.confrelid
            JOIN pg_namespace n2 ON n2.oid = c2.relnamespace
            WHERE k.contype = 'f' AND n1.nspname <> n2.nspname
            """);

        Assert.Empty(crossing);
    }

    // Mirrors each module's production registration, which keeps the migration history table in the
    // module's own schema.
    private static DbContextOptions<T> Options<T>(string connectionString, string schema) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .Options;

    private static async Task Migrate(DbContext context)
    {
        await using (context)
        {
            await context.Database.MigrateAsync();
        }
    }

    private async Task<List<string>> QueryStrings(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }
}
