using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

public sealed class MakeContributionCommandValidator : AbstractValidator<MakeContributionCommand>
{
    public MakeContributionCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);
    }
}
