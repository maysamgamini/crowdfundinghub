using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Contributions.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class ContributionsApplicationDependencyInjection
{
    public static IServiceCollection AddContributionsApplication(this IServiceCollection services)
    {
        services.AddScoped<ConfirmContributionPaymentCommandHandler>();
        services.AddScoped<FailContributionPaymentCommandHandler>();
        services.AddScoped<MakeContributionCommandHandler>();
        services.AddScoped<ListContributionsByCampaignQueryHandler>();
        services.AddScoped<IValidator<ConfirmContributionPaymentCommand>, ConfirmContributionPaymentCommandValidator>();
        services.AddScoped<IValidator<FailContributionPaymentCommand>, FailContributionPaymentCommandValidator>();
        services.AddScoped<IValidator<MakeContributionCommand>, MakeContributionCommandValidator>();

        return services;
    }
}
