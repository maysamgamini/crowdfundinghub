using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewStatusByCampaignId;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Moderation.Application.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class ModerationApplicationDependencyInjection
{
    public static IServiceCollection AddModerationApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateCampaignReviewCommandHandler>();
        services.AddScoped<ApproveCampaignReviewCommandHandler>();
        services.AddScoped<RejectCampaignReviewCommandHandler>();
        services.AddScoped<GetCampaignReviewByCampaignIdQueryHandler>();
        services.AddScoped<GetCampaignReviewStatusByCampaignIdQueryHandler>();
        services.AddScoped<IValidator<ApproveCampaignReviewCommand>, ApproveCampaignReviewCommandValidator>();
        services.AddScoped<IValidator<RejectCampaignReviewCommand>, RejectCampaignReviewCommandValidator>();

        return services;
    }
}
