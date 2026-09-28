using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Inbox;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Infrastructure.Inbox;
using CrowdFunding.Modules.Contributions.Infrastructure.Outbox;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Contributions.Infrastructure.Services;
using CrowdFunding.Modules.Contributions.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class ContributionsInfrastructureDependencyInjection
{
    public static IServiceCollection AddContributionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("ContributionsDb");

        // The interceptor wakes this module's outbox processor as soon as a transaction that wrote
        // outbox rows commits; the processor's poll interval is only the fallback sweep.
        services.AddOutboxSignal<ContributionsDbContext>();
        services.AddDbContext<ContributionsDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ContributionsDbContext.Schema))
                .AddOutboxSignalInterceptor<ContributionsDbContext>(serviceProvider));

        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<IContributionsIdempotentConsumer, ContributionsIdempotentConsumer>();
        services.AddScoped<IActiveCampaignCacheRepository, ActiveCampaignCacheRepository>();
        services.AddScoped<IContributionReadService, ContributionReadService>();
        services.AddScoped<IContributionTransactionExecutor, ContributionTransactionExecutor>();
        services.AddSingleton<IContributionDateTimeProvider, SystemDateTimeProvider>();
        services.AddHostedService<ContributionsOutboxBackgroundService>();

        return services;
    }
}
