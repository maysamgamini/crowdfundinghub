using FluentValidation;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;

/// <summary>
/// Validates Make Pledge Command instances before they reach the handler.
/// </summary>
public sealed class MakePledgeCommandValidator : AbstractValidator<MakePledgeCommand>
{
    public MakePledgeCommandValidator()
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
            .MaximumLength(Domain.Aggregates.Pledge.MaxIdempotencyKeyLength)
            .Matches("^[\\x21-\\x7E]+$")
            .WithMessage("The idempotency key must be 1 to 100 printable ASCII characters with no spaces.")
            .When(x => x.IdempotencyKey is not null);
    }
}
