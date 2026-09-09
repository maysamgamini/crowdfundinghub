using FluentValidation;

namespace CrowdFunding.Samples.RosettaStone.PragmaticCqrs;

/// <summary>
/// Tier 2: Pragmatic CQRS. Separates request validation from execution and dispatches through
/// the shared <c>ICommandDispatcher</c> like every other module — but persists directly against
/// <c>RosettaStoneDbContext</c> with no aggregate root, no domain events, and no outbox. The
/// right default for the 80% of enterprise CRUD that needs a validated, auditable write path
/// without a financial-grade consistency story behind it.
/// </summary>
public sealed record CreateCampaignCommand(string Title, string Story, decimal TargetAmount, string Currency);

public sealed record CreateCampaignResult(Guid Id);

public sealed class CreateCampaignCommandValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignCommandValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Story).NotEmpty().MaximumLength(5000);
        RuleFor(command => command.TargetAmount).GreaterThan(0);
        RuleFor(command => command.Currency).NotEmpty().Length(3);
    }
}
