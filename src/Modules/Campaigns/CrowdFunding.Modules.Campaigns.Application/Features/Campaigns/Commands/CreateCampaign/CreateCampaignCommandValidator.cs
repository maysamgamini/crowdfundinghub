using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

/// <summary>
/// Validates Create Campaign Command instances before they reach the handler.
/// </summary>
public sealed class CreateCampaignCommandValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Story)
            .NotEmpty()
            // Kept in sync with Campaign.ValidateStory's 20-character domain invariant — without
            // this, a too-short story passes validation here and only fails inside
            // Campaign.Create(), surfacing as an unstructured 400 instead of a field-level
            // ValidationProblemDetails error.
            .MinimumLength(20)
            .MaximumLength(5000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.GoalAmount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.DeadlineUtc)
            .Must(deadlineUtc => deadlineUtc > DateTime.UtcNow)
            .WithMessage("DeadlineUtc must be in the future.");
    }
}
