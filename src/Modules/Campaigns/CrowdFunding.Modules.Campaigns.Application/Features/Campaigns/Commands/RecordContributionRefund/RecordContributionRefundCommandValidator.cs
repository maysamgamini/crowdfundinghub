using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.RecordContributionRefund;

/// <summary>
/// Validates <see cref="RecordContributionRefundCommand"/>.
/// </summary>
public sealed class RecordContributionRefundCommandValidator : AbstractValidator<RecordContributionRefundCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordContributionRefundCommandValidator"/> class.
    /// </summary>
    public RecordContributionRefundCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ContributionId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
