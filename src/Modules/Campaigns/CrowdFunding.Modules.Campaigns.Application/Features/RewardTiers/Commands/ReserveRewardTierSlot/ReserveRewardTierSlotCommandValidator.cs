using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;

public sealed class ReserveRewardTierSlotCommandValidator : AbstractValidator<ReserveRewardTierSlotCommand>
{
    public ReserveRewardTierSlotCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.RewardTierId).NotEmpty();
    }
}
