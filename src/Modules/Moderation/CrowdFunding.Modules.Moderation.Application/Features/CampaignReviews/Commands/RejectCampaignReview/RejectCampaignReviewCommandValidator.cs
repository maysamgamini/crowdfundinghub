using FluentValidation;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

/// <summary>
/// Validates Reject Campaign Review Command instances before they reach the handler.
/// </summary>
public sealed class RejectCampaignReviewCommandValidator : AbstractValidator<RejectCampaignReviewCommand>
{
    public RejectCampaignReviewCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
    }
}
