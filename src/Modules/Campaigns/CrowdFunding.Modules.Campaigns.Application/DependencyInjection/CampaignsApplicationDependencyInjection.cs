using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignContributionAvailability;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class CampaignsApplicationDependencyInjection
{
    public static IServiceCollection AddCampaignsApplication(this IServiceCollection services)
    {
        services.AddScoped<AddContributionToCampaignCommandHandler>();
        services.AddScoped<CancelCampaignCommandHandler>();
        services.AddScoped<CreateCampaignCommandHandler>();
        services.AddScoped<PublishCampaignCommandHandler>();
        services.AddScoped<GetCampaignByIdQueryHandler>();
        services.AddScoped<GetCampaignContributionAvailabilityQueryHandler>();
        services.AddScoped<ListCampaignsQueryHandler>();
        services.AddScoped<IValidator<CancelCampaignCommand>, CancelCampaignCommandValidator>();
        services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();
        services.AddScoped<IValidator<PublishCampaignCommand>, PublishCampaignCommandValidator>();

        return services;
    }
}
