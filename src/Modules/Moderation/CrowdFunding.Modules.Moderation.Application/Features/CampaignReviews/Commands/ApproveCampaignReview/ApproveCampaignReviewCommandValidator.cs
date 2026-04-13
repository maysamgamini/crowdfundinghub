using FluentValidation;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

/// <summary>
/// Validates Approve Campaign Review Command instances before they reach the handler.
/// </summary>
public sealed class ApproveCampaignReviewCommandValidator : AbstractValidator<ApproveCampaignReviewCommand>
{
    public ApproveCampaignReviewCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}
