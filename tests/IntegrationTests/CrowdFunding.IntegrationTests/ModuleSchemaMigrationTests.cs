using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The <c>MoveTablesToModuleSchema</c> migrations on a database that already holds data in the
/// shared <c>public</c> schema: every row moves into its module's schema, nothing is left behind,
/// the ledger's triggers still enforce their invariants, and an outbox row written before the
/// upgrade is still claimed and delivered across modules afterwards.
/// </summary>
public sealed class ModuleSchemaMigrationTests : IAsyncLifetime
{
    private const string SchemaMoveMigrationSuffix = "_MoveTablesToModuleSchema";

    private static readonly Type[] ModuleDbContextTypes =
    [
        typeof(CampaignsDbContext),
        typeof(ContributionsDbContext),
        typeof(IdentityDbContext),
        typeof(ModerationDbContext),
        typeof(NotificationsDbContext),
        typeof(CampaignUpdatesDbContext),
    ];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Upgrade_ShouldMoveEveryRowIntoItsModuleSchema_AndKeepLedgerAndOutboxWorking()
    {
        // 1. The pre-change database: every module migrated up to, not including, its schema move.
        foreach (var contextType in ModuleDbContextTypes)
        {
            await using var dbContext = CreateDbContext(contextType);
            var migrations = dbContext.Database.GetMigrations().ToList();
            var moveIndex = migrations.FindIndex(name => name.EndsWith(SchemaMoveMigrationSuffix, StringComparison.Ordinal));
            Assert.True(moveIndex > 0, $"{contextType.Name} has no {SchemaMoveMigrationSuffix} migration.");
            await dbContext.GetService<IMigrator>().MigrateAsync(migrations[moveIndex - 1]);
        }

        // 2. Seed rows in public: a user with a role, a published campaign whose raised amount is
        //    backed by one ledger pledge, and a CampaignCreated event still pending in the outbox.
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var seededContributionId = Guid.NewGuid();
        const string title = "Campaign seeded before the schema move";
        var createdEvent = OutboxMessage.Create(
            new CampaignCreatedApplicationEvent(campaignId, userId, title, "USD", DateTime.UtcNow.AddDays(30)),
            DateTime.UtcNow);

        await using (var connection = await OpenConnectionAsync())
        {
            await ExecuteAsync(connection,
                """
                INSERT INTO public.users ("Id", email, normalized_email, display_name, password_hash, is_active, security_stamp, created_at_utc)
                VALUES (@user, 'seeded@example.com', 'SEEDED@EXAMPLE.COM', 'Seeded User', 'hash', true, gen_random_uuid(), now());
                INSERT INTO public.user_roles ("Id", role, user_id) VALUES (gen_random_uuid(), 'Creator', @user);

                INSERT INTO public.campaigns ("Id", "OwnerId", "Title", "Story", "Category", "DeadlineUtc", "Status", goal_amount, goal_currency, raised_amount, raised_currency, "CreatedAtUtc")
                VALUES (@campaign, @user, @title, 'A story that is long enough to be a campaign.', 'Technology', now() + interval '30 days', 2, 1000, 'USD', 0, 'USD', now());
                INSERT INTO public.campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
                VALUES (gen_random_uuid(), @campaign, @contribution, 0, 100, 'USD', now());
                UPDATE public.campaigns SET raised_amount = 100 WHERE "Id" = @campaign;

                INSERT INTO public.campaigns_outbox_messages ("Id", "EventType", "Version", "Payload", "Headers", "OccurredOnUtc", "ScheduledAtUtc", "Attempts", "Status")
                VALUES (@eventId, @eventType, @version, @payload, '{}', now(), now(), 0, 0);
                """,
                command =>
                {
                    command.Parameters.AddWithValue("user", userId);
                    command.Parameters.AddWithValue("campaign", campaignId);
                    command.Parameters.AddWithValue("contribution", seededContributionId);
                    command.Parameters.AddWithValue("title", title);
                    command.Parameters.AddWithValue("eventId", createdEvent.Id);
                    command.Parameters.AddWithValue("eventType", createdEvent.EventType);
                    command.Parameters.AddWithValue("version", createdEvent.Version);
                    command.Parameters.AddWithValue("payload", createdEvent.Payload);
                });
        }

        var countsBefore = await CountRowsAsync("public");
        Assert.Equal(1, countsBefore["campaigns"]);
        Assert.Equal(1, countsBefore["campaign_ledger_entries"]);
        Assert.Equal(1, countsBefore["campaigns_outbox_messages"]);
        Assert.Equal(1, countsBefore["users"]);
        Assert.Equal(1, countsBefore["user_roles"]);

        // 3. The upgrade.
        foreach (var contextType in ModuleDbContextTypes)
        {
            await using var dbContext = CreateDbContext(contextType);
            await dbContext.Database.MigrateAsync();
        }

        // Nothing is left in public, and every table sits in the schema its DbContext maps it to,
        // with exactly the rows it had before.
        Assert.Empty(await CountRowsAsync("public"));

        var countsAfter = new Dictionary<string, long>();
        foreach (var contextType in ModuleDbContextTypes)
        {
            await using var dbContext = CreateDbContext(contextType);
            var schema = dbContext.Model.GetDefaultSchema()!;
            var mappedTables = dbContext.Model.GetEntityTypes()
                .Select(entityType => entityType.GetTableName())
                .OfType<string>()
                .Append("__EFMigrationsHistory")
                .ToHashSet();

            var tablesInSchema = await CountRowsAsync(schema);
            Assert.Equal(mappedTables.Order(), tablesInSchema.Keys.Order());
            foreach (var (table, count) in tablesInSchema.Where(pair => pair.Key != "__EFMigrationsHistory"))
            {
                countsAfter.Add(table, count);
            }
        }

        Assert.Equal(countsBefore.OrderBy(pair => pair.Key), countsAfter.OrderBy(pair => pair.Key));

        // The ledger's trigger functions moved with their tables.
        await using (var connection = await OpenConnectionAsync())
        {
            await using var reader = await ReaderAsync(connection,
                """
                SELECT n.nspname FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE p.proname IN ('campaign_ledger_entries_reject_changes', 'campaigns_raised_amount_matches_ledger')
                """);
            var schemas = new List<string>();
            while (await reader.ReadAsync())
            {
                schemas.Add(reader.GetString(0));
            }

            Assert.Equal(2, schemas.Count);
            Assert.All(schemas, schema => Assert.Equal(CampaignsDbContext.Schema, schema));
        }

        // 4. The application on the upgraded database.
        await using var factory = CreateFactory();
        _ = factory.Server;

        // The event written to public before the upgrade is claimed from campaigns.campaigns_outbox_messages
        // and delivered to Moderation and Notifications, which write into their own schemas.
        await DrainOutboxesAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var campaigns = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
            Assert.False(await campaigns.OutboxMessages.AnyAsync(message => message.Id == createdEvent.Id));
            Assert.False(await campaigns.DeadLetterEvents.AnyAsync(letter => letter.SourceEventId == createdEvent.Id));

            var moderation = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            Assert.True(await moderation.CampaignReviews.AnyAsync(review => review.CampaignId == campaignId));

            var notifications = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            Assert.Equal(title, (await notifications.CampaignTitleCache.SingleAsync(cache => cache.CampaignId == campaignId)).Title);
        }

        // Ledger: a new pledge appends and recomputes the snapshot; a redelivery of it is rejected
        // by the moved unique index; the append-only and snapshot triggers still fire.
        var newContributionId = Guid.NewGuid();
        var credited = await PledgeAsync(factory, campaignId, newContributionId, 50m);
        Assert.True(credited.Credited);
        Assert.Equal(150m, credited.RaisedAmount);

        var redelivered = await PledgeAsync(factory, campaignId, newContributionId, 50m);
        Assert.False(redelivered.Credited);
        Assert.Equal(150m, redelivered.RaisedAmount);

        await using (var connection = await OpenConnectionAsync())
        {
            var tamper = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection,
                """UPDATE campaigns.campaign_ledger_entries SET "Amount" = 5000 WHERE "CampaignId" = @campaign""",
                command => command.Parameters.AddWithValue("campaign", campaignId)));
            Assert.Contains("append-only", tamper.MessageText);

            var drift = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection,
                """UPDATE campaigns.campaigns SET raised_amount = 1 WHERE "Id" = @campaign""",
                command => command.Parameters.AddWithValue("campaign", campaignId)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, drift.SqlState);

            await using var reader = await ReaderAsync(connection,
                """
                SELECT c.raised_amount, (SELECT SUM(e."Amount") FROM campaigns.campaign_ledger_entries e WHERE e."CampaignId" = c."Id")
                FROM campaigns.campaigns c WHERE c."Id" = @campaign
                """,
                command => command.Parameters.AddWithValue("campaign", campaignId));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(150m, reader.GetDecimal(0));
            Assert.Equal(150m, reader.GetDecimal(1));
        }
    }

    private DbContext CreateDbContext(Type contextType)
    {
        var schema = (string)contextType.GetField("Schema")!.GetValue(null)!;
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType))!;
        builder.UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema));
        return (DbContext)Activator.CreateInstance(contextType, builder.Options)!;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Row count of every base table in <paramref name="schema"/>, keyed by table name.</summary>
    private async Task<Dictionary<string, long>> CountRowsAsync(string schema)
    {
        await using var connection = await OpenConnectionAsync();
        var tables = new List<string>();
        await using (var reader = await ReaderAsync(connection,
                         "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
                         command => command.Parameters.AddWithValue("schema", schema)))
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var counts = new Dictionary<string, long>();
        foreach (var table in tables)
        {
            // Identifiers come from the catalog, not from input.
            await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{schema}\".\"{table}\"", connection);
            counts[table] = (long)(await command.ExecuteScalarAsync())!;
        }

        return counts;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, Action<NpgsqlCommand>? configure = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<NpgsqlDataReader> ReaderAsync(NpgsqlConnection connection, string sql, Action<NpgsqlCommand>? configure = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        configure?.Invoke(command);
        return await command.ExecuteReaderAsync();
    }

    private static async Task<AddContributionToCampaignResult> PledgeAsync(
        WebApplicationFactory<Program> factory, Guid campaignId, Guid contributionId, decimal amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        return await dispatcher.SendAsync<AddContributionToCampaignResult>(
            new AddContributionToCampaignCommand(campaignId, contributionId, amount, "USD"),
            CancellationToken.None);
    }

    private static async Task DrainOutboxesAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        foreach (var dispatcher in scope.ServiceProvider.GetServices<IHostedService>().OfType<IOutboxDispatcher>())
        {
            await dispatcher.DrainAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CrowdFunding.Test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "CrowdFunding.Test.Client");
        Environment.SetEnvironmentVariable("SigningKeyEncryption__Key", "jtLCVz1aVwU89pIYA7XsdoffmhlWnw/oS5Huy+aSX00=");
        return new UpgradedDatabaseFactory();
    }

    private sealed class UpgradedDatabaseFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
