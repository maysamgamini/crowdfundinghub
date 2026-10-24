using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmPledgeSetup;

public sealed class ConfirmPledgeSetupCommandValidator : AbstractValidator<ConfirmPledgeSetupCommand>
{
    public ConfirmPledgeSetupCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ContributionId).NotEmpty();
        RuleFor(x => x.PaymentMethodReference).NotEmpty().MaximumLength(100);
    }
}
