using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Notifications.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Notifications.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Notifications.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class NotificationsInfrastructureDependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("NotificationsDb");

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

        services.AddScoped<ICampaignTitleCacheRepository, CampaignTitleCacheRepository>();
        services.AddSingleton<IEmailNotificationSink, InMemoryEmailNotificationSink>();

        var emailOptions = configuration.GetSection(EmailProviderOptions.SectionName).Get<EmailProviderOptions>()
            ?? new EmailProviderOptions();

        if (string.Equals(emailOptions.EmailProvider, "Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(emailOptions);
            services.AddHttpClient<IEmailNotificationService, HttpEmailNotificationService>(client =>
                {
                    client.BaseAddress = emailOptions.BaseUrl;

                    if (!string.IsNullOrWhiteSpace(emailOptions.ApiKey))
                    {
                        client.DefaultRequestHeaders.Authorization = new("Bearer", emailOptions.ApiKey);
                    }
                })
                .AddStandardResilienceHandler();
        }
        else
        {
            services.AddScoped<IEmailNotificationService, LoggingEmailNotificationService>();
        }

        return services;
    }
}
