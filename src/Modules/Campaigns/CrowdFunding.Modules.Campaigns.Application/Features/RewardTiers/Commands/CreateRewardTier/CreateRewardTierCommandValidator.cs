using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;

public sealed class CreateRewardTierCommandValidator : AbstractValidator<CreateRewardTierCommand>
{
    public CreateRewardTierCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.MinimumPledgeAmount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.TotalCapacity).GreaterThan(0);
    }
}
