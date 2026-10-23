using Microsoft.Extensions.DependencyInjection.Extensions;
using CrowdFunding.Modules.Contributions.Infrastructure.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
﻿using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
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

        services.AddDbContext<ContributionsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "contributions")));

        services.AddInbox<ContributionsDbContext>(typeof(CrowdFunding.Modules.Contributions.Application.DependencyInjection.ContributionsApplicationDependencyInjection).Assembly);

        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<IPaymentWebhookIdempotencyStore, PaymentWebhookIdempotencyStore>();
        services.AddScoped<IActiveCampaignCacheRepository, ActiveCampaignCacheRepository>();
        services.AddScoped<IContributionReadService, ContributionReadService>();
        services.AddScoped<IContributionTransactionExecutor, ContributionTransactionExecutor>();
        services.AddSingleton<IContributionDateTimeProvider, SystemDateTimeProvider>();
        services.AddRetention();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRetentionTask, IdempotencyKeyExpiryTask>());
        services.AddOutboxSignal<ContributionsDbContext>();
        services.AddHostedService<ContributionsOutboxBackgroundService>();

        return services;
    }
}
