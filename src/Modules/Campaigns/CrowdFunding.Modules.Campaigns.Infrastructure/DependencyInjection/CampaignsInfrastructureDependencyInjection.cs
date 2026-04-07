using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Contracts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Campaigns.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.DependencyInjection;

public static class CampaignsInfrastructureDependencyInjection
{
    public static IServiceCollection AddCampaignsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<CampaignsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICampaignsModule, CampaignsModule>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ICampaignReadService, CampaignReadService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
