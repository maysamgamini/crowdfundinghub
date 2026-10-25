using FluentValidation;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;

/// <summary>
/// Validates Confirm Pledge Payment Command instances before they reach the handler.
/// </summary>
public sealed class ConfirmPledgePaymentCommandValidator : AbstractValidator<ConfirmPledgePaymentCommand>
{
    public ConfirmPledgePaymentCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.PledgeId).NotEmpty();
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
    }
}
