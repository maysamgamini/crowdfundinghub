using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Infrastructure.Caching;
using CrowdFunding.Modules.Campaigns.Infrastructure.Outbox;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Services;
using CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class CampaignsInfrastructureDependencyInjection
{
    public static IServiceCollection AddCampaignsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("CampaignsDb");
        var redisConnectionString = configuration.GetConnectionString("Redis")
                                    ?? throw new InvalidOperationException("Connection string 'Redis' was not found.");

        services.AddDbContext<CampaignsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "campaigns")));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "crowdfunding:";
        });

        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IContributionLedger, ContributionLedger>();
        services.AddScoped<CampaignReadService>();
        services.AddScoped<ICampaignReadService, CachedCampaignReadService>(sp =>
            new CachedCampaignReadService(
                sp.GetRequiredService<CampaignReadService>(),
                sp.GetRequiredService<IDistributedCache>(),
                sp.GetRequiredService<ILogger<CachedCampaignReadService>>()));
        services.AddScoped<ICampaignTransactionExecutor, CampaignTransactionExecutor>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddHostedService<CampaignsOutboxBackgroundService>();

        return services;
    }
}
