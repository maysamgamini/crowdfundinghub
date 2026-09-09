using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Infrastructure.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Registers the <see cref="IMessageBus"/> implementation selected by
/// <c>Messaging:Provider</c> ("InProcess" — the default, or "RabbitMQ").
/// </summary>
public static class MessagingDependencyInjection
{
    public static IServiceCollection AddCrowdFundingMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEventPublisher, ServiceProviderEventPublisher>();

        var provider = configuration["Messaging:Provider"] ?? "InProcess";

        if (provider.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
            services.AddSingleton<RabbitMqMessageBus>();
            services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        }
        else
        {
            services.AddScoped<IMessageBus, InProcessMessageBus>();
        }

        return services;
    }
}
