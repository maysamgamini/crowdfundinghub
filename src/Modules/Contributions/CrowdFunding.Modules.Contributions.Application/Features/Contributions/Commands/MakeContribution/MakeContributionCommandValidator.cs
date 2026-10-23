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

        // Optional. Printable ASCII only, so a key can travel in a header and a log line safely.
        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(Domain.Aggregates.Contribution.MaxIdempotencyKeyLength)
            .Matches("^[\\x21-\\x7E]+$")
            .WithMessage("The idempotency key must be 1 to 100 printable ASCII characters with no spaces.")
            .When(x => x.IdempotencyKey is not null);
    }
}
