using FluentValidation;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

public sealed class ApproveCampaignReviewCommandValidator : AbstractValidator<ApproveCampaignReviewCommand>
{
    public ApproveCampaignReviewCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}
