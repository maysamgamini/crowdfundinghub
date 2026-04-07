using FluentValidation;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignCommandValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignCommandValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Story)
            .NotEmpty()
            .MinimumLength(20);

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
