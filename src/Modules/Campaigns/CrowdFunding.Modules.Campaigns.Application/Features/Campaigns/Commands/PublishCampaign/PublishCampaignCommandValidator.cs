using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;

public sealed class PublishCampaignCommandValidator : AbstractValidator<PublishCampaignCommand>
{
    public PublishCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty();
    }
}
