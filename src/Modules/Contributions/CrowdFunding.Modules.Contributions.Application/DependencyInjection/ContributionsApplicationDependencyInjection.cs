using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Contributions.Application.DependencyInjection;

public static class ContributionsApplicationDependencyInjection
{
    public static IServiceCollection AddContributionsApplication(this IServiceCollection services)
    {
        services.AddScoped<MakeContributionCommandHandler>();
        services.AddScoped<ListContributionsByCampaignQueryHandler>();
        services.AddScoped<IValidator<MakeContributionCommand>, MakeContributionCommandValidator>();

        return services;
    }
}
