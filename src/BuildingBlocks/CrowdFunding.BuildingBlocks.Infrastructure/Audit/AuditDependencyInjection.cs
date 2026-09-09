using CrowdFunding.BuildingBlocks.Application.Audit;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// Registers the cross-cutting audit-logging pipeline behavior (TICKET-039) and its storage.
/// </summary>
public static class AuditDependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("AuditDb");

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "system")));

        services.AddScoped<IAuditStore, EfAuditStore>();

        // Open-generic registration: applies to every TCommand/TResult pair the command
        // dispatcher resolves, so no module registers this per-command — it is truly global.
        services.AddScoped(typeof(ICommandPipelineBehavior<,>), typeof(AuditLoggingPipelineBehavior<,>));

        return services;
    }
}
