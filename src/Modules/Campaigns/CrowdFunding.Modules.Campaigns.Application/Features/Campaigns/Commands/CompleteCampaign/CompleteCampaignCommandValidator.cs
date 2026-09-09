using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;

/// <summary>
/// Validates Complete Campaign Command instances before they reach the handler.
/// </summary>
public sealed class CompleteCampaignCommandValidator : AbstractValidator<CompleteCampaignCommand>
{
    public CompleteCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty();
    }
}
