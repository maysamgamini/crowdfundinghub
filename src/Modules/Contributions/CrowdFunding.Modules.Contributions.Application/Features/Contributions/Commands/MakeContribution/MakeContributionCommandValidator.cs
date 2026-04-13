using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

/// <summary>
/// Validates Make Contribution Command instances before they reach the handler.
/// </summary>
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
