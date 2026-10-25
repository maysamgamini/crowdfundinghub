using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrowdFunding.Modules.Pledges.Infrastructure.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Outbox;
﻿using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Infrastructure.Outbox;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Pledges.Infrastructure.Services;
using CrowdFunding.Modules.Pledges.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Pledges.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class PledgesInfrastructureDependencyInjection
{
    public static IServiceCollection AddPledgesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("PledgesDb");

        services.AddDbContext<PledgesDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "pledges")));

        services.AddInbox<PledgesDbContext>(typeof(CrowdFunding.Modules.Pledges.Application.DependencyInjection.PledgesApplicationDependencyInjection).Assembly);

        services.AddScoped<IPledgeRepository, PledgeRepository>();
        services.AddScoped<IPaymentWebhookIdempotencyStore, PaymentWebhookIdempotencyStore>();
        services.AddScoped<IActiveCampaignCacheRepository, ActiveCampaignCacheRepository>();
        services.AddScoped<IPledgeReadService, PledgeReadService>();
        services.AddScoped<IPledgeTransactionExecutor, PledgeTransactionExecutor>();
        services.AddScoped<IPledgeSettlementStore, PledgeSettlementStore>();
        services.AddScoped<IPayoutStore, PayoutStore>();
        services.AddOptions<PaymentOptions>().BindConfiguration(PaymentOptions.SectionName);
        services.AddSingleton<IPaymentSettings>(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentOptions>>().Value);
        services.AddSingleton<FakePaymentProvider>();
        services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<FakePaymentProvider>());
        services.AddSingleton<IPledgeDateTimeProvider, SystemDateTimeProvider>();
        services.AddRetention();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRetentionTask, IdempotencyKeyExpiryTask>());
        services.AddOutboxSignal<PledgesDbContext>();
        services.AddHostedService<PledgesOutboxBackgroundService>();
        services.AddHostedService<SettlementBackgroundService>();

        return services;
    }
}
