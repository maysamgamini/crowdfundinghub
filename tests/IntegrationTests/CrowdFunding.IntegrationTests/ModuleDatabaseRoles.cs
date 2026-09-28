using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// One module's runtime database role: the connection string key the module reads, the schema it
/// owns, the login role that may use only that schema, and the connection-pool ceiling.
/// </summary>
/// <param name="ConnectionStringName">The <c>ConnectionStrings</c> key the module reads (e.g. <c>CampaignsDb</c>).</param>
/// <param name="Schema">The schema the role may use.</param>
/// <param name="Role">The login role.</param>
/// <param name="MaxPoolSize">The <c>Maximum Pool Size</c> of the module's connection string.</param>
public sealed record ModuleDatabaseRole(string ConnectionStringName, string Schema, string Role, int MaxPoolSize);

/// <summary>
/// Provisions the per-module database roles in a Testcontainers PostgreSQL instance with the same
/// scripts docker compose runs (<c>db/init/*.sql</c>, copied to the test output), and points the
/// application's module connection strings at those roles, so the suite runs with the database
/// enforcing data ownership exactly as a deployed environment does.
/// </summary>
public static class ModuleDatabaseRoles
{
    /// <summary>The owner role that runs migrations.</summary>
    public const string MigratorRole = "crowdfunding_migrator";

    /// <summary>Every runtime role, one per module plus the audit writer (the same pool sizes as appsettings.Development.json).</summary>
    public static readonly IReadOnlyList<ModuleDatabaseRole> All =
    [
        new("CampaignsDb", "campaigns", "campaigns_app", 10),
        new("ContributionsDb", "contributions", "contributions_app", 10),
        new("IdentityDb", "identity", "identity_app", 8),
        new("ModerationDb", "moderation", "moderation_app", 5),
        new("NotificationsDb", "notifications", "notifications_app", 5),
        new("CampaignUpdatesDb", "campaignupdates", "campaignupdates_app", 5),
        new("AuditDb", "system", "audit_app", 3),
    ];

    /// <summary>
    /// Runs <c>db/init/01-module-roles.sql</c> and <c>02-dev-passwords.sql</c> as the container's
    /// superuser, before any migration, as docker compose does on a fresh volume.
    /// </summary>
    /// <param name="superuserConnectionString">The container's superuser connection string.</param>
    /// <returns>A task that completes when the roles exist.</returns>
    public static async Task ProvisionAsync(string superuserConnectionString)
    {
        var scripts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "db", "init"), "*.sql").Order().ToList();
        Assert.NotEmpty(scripts);

        await using var connection = new NpgsqlConnection(superuserConnectionString);
        await connection.OpenAsync();
        foreach (var script in scripts)
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>The development password <c>02-dev-passwords.sql</c> gives a role.</summary>
    /// <param name="role">The role name.</param>
    /// <returns>The password.</returns>
    public static string DevPassword(string role) => $"{role}_dev";

    /// <summary>A connection string for <paramref name="role"/> on the same server and database.</summary>
    /// <param name="superuserConnectionString">The container's superuser connection string.</param>
    /// <param name="role">The login role.</param>
    /// <param name="maxPoolSize">The pool ceiling, or <see langword="null"/> for the driver default.</param>
    /// <returns>The connection string.</returns>
    public static string ConnectionStringFor(string superuserConnectionString, string role, int? maxPoolSize = null)
    {
        var builder = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = role,
            Password = DevPassword(role),
        };

        if (maxPoolSize is not null)
        {
            builder.MaxPoolSize = maxPoolSize.Value;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Points every module (and the migration runner) at its role through process environment
    /// variables, which <c>Program</c> reads while the test host is built.
    /// </summary>
    /// <param name="superuserConnectionString">The container's superuser connection string.</param>
    public static void UseInEnvironment(string superuserConnectionString)
    {
        foreach (var module in All)
        {
            Environment.SetEnvironmentVariable(
                $"ConnectionStrings__{module.ConnectionStringName}",
                ConnectionStringFor(superuserConnectionString, module.Role, module.MaxPoolSize));
        }

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Migrations",
            ConnectionStringFor(superuserConnectionString, MigratorRole));
    }

    /// <summary>
    /// Removes the per-module variables, so a later host in the same test process falls back to
    /// <c>ConnectionStrings:DefaultConnection</c> instead of a disposed container.
    /// </summary>
    public static void ClearEnvironment()
    {
        foreach (var module in All)
        {
            Environment.SetEnvironmentVariable($"ConnectionStrings__{module.ConnectionStringName}", null);
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Migrations", null);
    }
}
