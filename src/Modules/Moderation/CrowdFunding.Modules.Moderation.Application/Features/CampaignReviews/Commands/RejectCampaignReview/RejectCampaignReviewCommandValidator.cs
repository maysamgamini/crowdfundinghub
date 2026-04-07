using FluentValidation;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

public sealed class RejectCampaignReviewCommandValidator : AbstractValidator<RejectCampaignReviewCommand>
{
    public RejectCampaignReviewCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}
