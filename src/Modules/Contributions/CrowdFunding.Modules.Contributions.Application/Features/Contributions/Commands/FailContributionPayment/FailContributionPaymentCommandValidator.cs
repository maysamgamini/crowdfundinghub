using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

public sealed class FailContributionPaymentCommandValidator : AbstractValidator<FailContributionPaymentCommand>
{
    public FailContributionPaymentCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ContributionId).NotEmpty();
        RuleFor(x => x.FailureReason).NotEmpty().MaximumLength(500);
    }
}
