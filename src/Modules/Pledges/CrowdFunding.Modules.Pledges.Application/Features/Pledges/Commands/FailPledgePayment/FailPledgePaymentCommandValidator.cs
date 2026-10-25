using FluentValidation;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;

/// <summary>
/// Validates Fail Pledge Payment Command instances before they reach the handler.
/// </summary>
public sealed class FailPledgePaymentCommandValidator : AbstractValidator<FailPledgePaymentCommand>
{
    public FailPledgePaymentCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.PledgeId).NotEmpty();
        RuleFor(x => x.FailureReason).NotEmpty().MaximumLength(500);
    }
}
