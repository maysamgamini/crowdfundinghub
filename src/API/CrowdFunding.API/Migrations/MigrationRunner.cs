using CrowdFunding.BuildingBlocks.Infrastructure.Audit;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CrowdFunding.API.Migrations;

/// <summary>
/// Applies every module's EF Core migrations from a CLI invocation (<c>dotnet run -- migrate</c>),
/// so staging/production deployments and container init-jobs have an explicit, one-shot
/// migration step instead of relying on <c>app.Environment.IsDevelopment()</c> to run migrations
/// implicitly on every app instance boot (improvement.md §2.7 — that gate meant migrations never
/// ran outside Development, and running them on every replica boot in production would race
/// multiple instances issuing concurrent DDL against the same tables).
/// </summary>
/// <remarks>
/// Migrations run as the schema owner, not as the module roles the application uses at run time:
/// when <c>ConnectionStrings:Migrations</c> is set (the <c>crowdfunding_migrator</c> role created
/// by <c>db/init/01-module-roles.sql</c>), each context migrates with that string's user name and
/// password, so the runtime roles never need DDL rights. Only the credentials are taken from it:
/// host and database stay those of the module's own connection string, so a module pointed at
/// its own database is still migrated there. Without it, each context migrates over its own
/// connection string (the single-superuser setup). See docs/data-ownership.md.
/// </remarks>
public static class MigrationRunner
{
    /// <summary>The connection string key for the owner role that runs migrations.</summary>
    public const string MigrationsConnectionStringName = "Migrations";

    /// <summary>
    /// Executes EF Core migrations sequentially across all module DbContexts.
    /// </summary>
    /// <param name="services">The application root service provider.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous migration operation.</returns>
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationRunner");
        var ownerConnectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>()
            .GetConnectionString(MigrationsConnectionStringName);

        await MigrateAsync<CampaignsDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<ContributionsDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<IdentityDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<ModerationDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<NotificationsDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<CampaignUpdatesDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
        await MigrateAsync<AuditDbContext>(scope.ServiceProvider, ownerConnectionString, logger, cancellationToken);
    }

    private static async Task MigrateAsync<TContext>(
        IServiceProvider serviceProvider,
        string? ownerConnectionString,
        ILogger logger,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var name = typeof(TContext).Name;
        var dbContext = serviceProvider.GetRequiredService<TContext>();

        if (!string.IsNullOrWhiteSpace(ownerConnectionString))
        {
            // Same server and database as the module, different role: DDL runs as the owner.
            var owner = new NpgsqlConnectionStringBuilder(ownerConnectionString);
            var migrationConnection = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString())
            {
                Username = owner.Username,
                Password = owner.Password,
            };
            dbContext.Database.SetConnectionString(migrationConnection.ConnectionString);
        }

        logger.LogInformation("Applying migrations for {DbContext}...", name);

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied successfully for {DbContext}.", name);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            logger.LogError(ex, "Schema divergence detected for {DbContext}. Table already exists.", name);
            throw new InvalidOperationException($"Database migration failed due to schema divergence in {name}.", ex);
        }
    }
}
