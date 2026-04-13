using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

/// <summary>
/// Validates Publish Campaign Command instances before they reach the handler.
/// </summary>
public sealed class PublishCampaignCommandValidator : AbstractValidator<PublishCampaignCommand>
{
    public PublishCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty();
    }
}
