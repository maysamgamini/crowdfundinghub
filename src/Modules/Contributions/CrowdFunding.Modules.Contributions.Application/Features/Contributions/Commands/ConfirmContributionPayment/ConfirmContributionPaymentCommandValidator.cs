using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

/// <summary>
/// Validates Confirm Contribution Payment Command instances before they reach the handler.
/// </summary>
public sealed class ConfirmContributionPaymentCommandValidator : AbstractValidator<ConfirmContributionPaymentCommand>
{
    public ConfirmContributionPaymentCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ContributionId).NotEmpty();
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
    }
}
