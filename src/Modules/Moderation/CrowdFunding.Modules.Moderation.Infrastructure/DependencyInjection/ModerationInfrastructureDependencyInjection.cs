using CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Moderation.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Moderation.Infrastructure.DependencyInjection;

public static class ModerationInfrastructureDependencyInjection
{
    public static IServiceCollection AddModerationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ModerationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICampaignReviewRepository, CampaignReviewRepository>();
        services.AddScoped<ICampaignReviewReadService, CampaignReviewReadService>();
        services.AddSingleton<IModerationDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
