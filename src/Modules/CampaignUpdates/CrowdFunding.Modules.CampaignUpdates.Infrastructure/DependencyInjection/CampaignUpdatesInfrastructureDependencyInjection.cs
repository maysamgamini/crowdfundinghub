using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class CampaignUpdatesInfrastructureDependencyInjection
{
    public static IServiceCollection AddCampaignUpdatesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("CampaignUpdatesDb");

        services.AddDbContext<CampaignUpdatesDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "campaignupdates")));

        services.AddScoped<ICampaignOwnerCacheRepository, CampaignOwnerCacheRepository>();
        services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
        services.AddScoped<IWebhookDeliveryTaskRepository, WebhookDeliveryTaskRepository>();
        services.AddSingleton<ICampaignUpdatesDateTimeProvider, SystemDateTimeProvider>();

        // A named client rather than a typed client: the dispatcher calls whatever URL each
        // subscription holds, not one fixed base address.
        services.AddHttpClient(nameof(WebhookDispatcherBackgroundService));
        services.AddSingleton<WebhookDispatcherBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<WebhookDispatcherBackgroundService>());

        return services;
    }
}
