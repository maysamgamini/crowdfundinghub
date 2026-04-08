using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Contributions.Infrastructure.Services;
using CrowdFunding.Modules.Contributions.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Contributions.Infrastructure.DependencyInjection;

public static class ContributionsInfrastructureDependencyInjection
{
    public static IServiceCollection AddContributionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ContributionsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<IContributionReadService, ContributionReadService>();
        services.AddScoped<IContributionTransactionExecutor, ContributionTransactionExecutor>();
        services.AddSingleton<IContributionDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
