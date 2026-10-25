using FluentValidation;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgeSetup;

public sealed class ConfirmPledgeSetupCommandValidator : AbstractValidator<ConfirmPledgeSetupCommand>
{
    public ConfirmPledgeSetupCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.PledgeId).NotEmpty();
        RuleFor(x => x.PaymentMethodReference).NotEmpty().MaximumLength(100);
    }
}
