using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Campaigns.Application.DependencyInjection;

public static class CampaignsApplicationDependencyInjection
{
    public static IServiceCollection AddCampaignsApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateCampaignCommandHandler>();
        services.AddScoped<GetCampaignByIdQueryHandler>();
        services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();

        return services;
    }
}
