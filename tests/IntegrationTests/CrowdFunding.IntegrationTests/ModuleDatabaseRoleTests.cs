using CrowdFunding.BuildingBlocks.Infrastructure.Audit;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The database enforces data ownership: each module's role can read and write its own schema and
/// is refused (<c>42501 insufficient_privilege</c>) everywhere else; no runtime role can run DDL,
/// truncate, or switch off the ledger's triggers; the audit writer can only append. Runs against
/// the shared API factory, whose database was provisioned by <c>db/init/*.sql</c> and migrated by
/// the owner role, so every other test in the collection also runs under these roles.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class ModuleDatabaseRoleTests
{
    private readonly CrowdFundingApiFactory _factory;

    public ModuleDatabaseRoleTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryModuleRole_ShouldReadAndWriteEveryTableOfItsOwnSchema()
    {
        var firstColumns = await FirstColumnsAsync();
        foreach (var module in ModuleDatabaseRoles.All)
        {
            await using var connection = await OpenAsRoleAsync(module.Role);
            foreach (var table in TablesBySchema()[module.Schema])
            {
                // Privileges are checked when the statement starts, so WHERE false proves the
                // right without changing data (and without firing row triggers).
                await ExecuteAsync(connection, $"SELECT COUNT(*) FROM {table}");
                await ExecuteAsync(connection, $"INSERT INTO {table} SELECT * FROM {table} WHERE false");
                if (module.Schema != "system")
                {
                    await ExecuteAsync(connection, $"UPDATE {table} SET \"{firstColumns[table]}\" = \"{firstColumns[table]}\" WHERE false");
                    await ExecuteAsync(connection, $"DELETE FROM {table} WHERE false");
                }
            }
        }
    }

    [Fact]
    public async Task EveryModuleRole_ShouldBeRefused_OnEveryOtherModulesSchema()
    {
        var tablesBySchema = TablesBySchema();
        var allowed = new List<string>();

        foreach (var module in ModuleDatabaseRoles.All)
        {
            await using var connection = await OpenAsRoleAsync(module.Role);
            foreach (var table in tablesBySchema.Where(pair => pair.Key != module.Schema).SelectMany(pair => pair.Value))
            {
                foreach (var sql in new[]
                         {
                             $"SELECT 1 FROM {table} LIMIT 1",
                             $"INSERT INTO {table} SELECT * FROM {table} WHERE false",
                             $"DELETE FROM {table} WHERE false",
                         })
                {
                    var error = await Record.ExceptionAsync(() => ExecuteAsync(connection, sql));
                    if (error is not PostgresException { SqlState: PostgresErrorCodes.InsufficientPrivilege })
                    {
                        allowed.Add($"{module.Role}: {sql} -> {error?.Message ?? "succeeded"}");
                    }
                }
            }
        }

        Assert.Empty(allowed);
    }

    [Fact]
    public async Task ModuleRoles_ShouldNotRunDdl_NorBypassTheLedgerTriggers()
    {
        await using var connection = await OpenAsRoleAsync("campaigns_app");

        foreach (var sql in new[]
                 {
                     "CREATE TABLE campaigns.shadow_ledger (id int)",
                     "CREATE TABLE public.shadow_ledger (id int)",
                     "CREATE SCHEMA shadow",
                     "TRUNCATE campaigns.campaign_ledger_entries",
                     "ALTER TABLE campaigns.campaign_ledger_entries DISABLE TRIGGER campaign_ledger_entries_append_only",
                     "ALTER TABLE campaigns.campaigns DISABLE TRIGGER USER",
                     "DROP TRIGGER campaigns_raised_amount_on_update ON campaigns.campaigns",
                     "DROP TABLE campaigns.campaign_ledger_entries",
                     "SET session_replication_role = replica",
                     "DELETE FROM campaigns.\"__EFMigrationsHistory\"",
                 })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }

        // The role may append to the ledger, and the triggers still hold it to the rules.
        var campaignId = Guid.NewGuid();
        await using var transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection,
            $"""
             INSERT INTO campaigns.campaigns ("Id", "OwnerId", "Title", "Story", "Category", "DeadlineUtc", "Status", goal_amount, goal_currency, raised_amount, raised_currency, "CreatedAtUtc")
             VALUES ('{campaignId}', gen_random_uuid(), 'Role test', 'A story that is long enough to be a campaign.', 'Technology', now() + interval '30 days', 2, 1000, 'USD', 0, 'USD', now());
             INSERT INTO campaigns.campaign_ledger_entries ("Id", "CampaignId", "ContributionId", "Kind", "Amount", "Currency", "RecordedAtUtc")
             VALUES (gen_random_uuid(), '{campaignId}', gen_random_uuid(), 0, 25, 'USD', now());
             """);

        var tamper = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection,
            $"""UPDATE campaigns.campaign_ledger_entries SET "Amount" = 5000 WHERE "CampaignId" = '{campaignId}'"""));
        Assert.Contains("append-only", tamper.MessageText);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task AuditRole_ShouldOnlyAppendToTheAuditTrail()
    {
        await using var connection = await OpenAsRoleAsync("audit_app");
        var recordId = Guid.NewGuid();

        await ExecuteAsync(connection,
            "INSERT INTO system.audit_records (id, actor_id, actor_email, action, command_type, payload_json, ip_address, user_agent, timestamp_utc) " +
            $"VALUES ('{recordId}', gen_random_uuid(), 'role@example.com', 'Role.Test', 'RoleTestCommand', '{{}}', '127.0.0.1', 'role-test', now())");
        await ExecuteAsync(connection, $"SELECT 1 FROM system.audit_records WHERE id = '{recordId}'");

        foreach (var sql in new[]
                 {
                     $"UPDATE system.audit_records SET action = 'Tampered' WHERE id = '{recordId}'",
                     $"DELETE FROM system.audit_records WHERE id = '{recordId}'",
                     "TRUNCATE system.audit_records",
                 })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
    }

    [Fact]
    public async Task EveryModuleObject_ShouldBeOwnedByTheMigrator_AndNoRuntimeRoleShouldOwnAnything()
    {
        await using var connection = new NpgsqlConnection(SuperuserConnectionString());
        await connection.OpenAsync();

        var schemas = string.Join(", ", ModuleDatabaseRoles.All.Select(module => $"'{module.Schema}'"));
        var roles = string.Join(", ", ModuleDatabaseRoles.All.Select(module => $"'{module.Role}'"));

        await using var command = new NpgsqlCommand(
            $"""
             SELECT n.nspname || '.' || c.relname || ' owned by ' || pg_get_userbyid(c.relowner)
             FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE (n.nspname IN ({schemas}) AND c.relkind IN ('r', 'p', 'S', 'v', 'm')
                    AND pg_get_userbyid(c.relowner) <> '{ModuleDatabaseRoles.MigratorRole}')
                OR pg_get_userbyid(c.relowner) IN ({roles})
             UNION ALL
             SELECT 'schema ' || nspname || ' owned by ' || pg_get_userbyid(nspowner)
             FROM pg_namespace
             WHERE nspname IN ({schemas}) AND pg_get_userbyid(nspowner) <> '{ModuleDatabaseRoles.MigratorRole}'
             """,
            connection);
        var violations = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                violations.Add(reader.GetString(0));
            }
        }

        Assert.Empty(violations);
    }

    /// <summary>Every module table, schema-qualified and quoted, grouped by schema, from the EF models the host uses.</summary>
    private Dictionary<string, List<string>> TablesBySchema()
    {
        using var scope = _factory.Services.CreateScope();
        var contexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<CampaignsDbContext>(),
            scope.ServiceProvider.GetRequiredService<ContributionsDbContext>(),
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
            scope.ServiceProvider.GetRequiredService<ModerationDbContext>(),
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<CampaignUpdatesDbContext>(),
            scope.ServiceProvider.GetRequiredService<AuditDbContext>(),
        };

        return contexts
            .SelectMany(context => context.Model.GetEntityTypes())
            .Where(entityType => entityType.GetTableName() is not null)
            .Select(entityType => (Schema: entityType.GetSchema()!, Table: $"\"{entityType.GetSchema()}\".\"{entityType.GetTableName()}\""))
            .Distinct()
            .GroupBy(pair => pair.Schema, pair => pair.Table)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    private string SuperuserConnectionString()
        => _factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

    private async Task<NpgsqlConnection> OpenAsRoleAsync(string role)
    {
        var connection = new NpgsqlConnection(ModuleDatabaseRoles.ConnectionStringFor(SuperuserConnectionString(), role));
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>One real column per table (keyed like <see cref="TablesBySchema"/>), so the UPDATE probe is valid SQL for every table.</summary>
    private async Task<Dictionary<string, string>> FirstColumnsAsync()
    {
        var firstColumns = new Dictionary<string, string>();
        await using var connection = new NpgsqlConnection(SuperuserConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT '"' || table_schema || '"."' || table_name || '"', column_name
            FROM information_schema.columns WHERE ordinal_position = 1
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            firstColumns[reader.GetString(0)] = reader.GetString(1);
        }

        return firstColumns;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
