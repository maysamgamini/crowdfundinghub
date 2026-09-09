using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.FailCampaign;

/// <summary>
/// Validates Fail Campaign Command instances before they reach the handler.
/// </summary>
public sealed class FailCampaignCommandValidator : AbstractValidator<FailCampaignCommand>
{
    public FailCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty();
    }
}
