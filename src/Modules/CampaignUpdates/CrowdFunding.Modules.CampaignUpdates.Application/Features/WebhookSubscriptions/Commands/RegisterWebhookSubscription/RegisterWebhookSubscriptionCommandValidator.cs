using FluentValidation;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;

public sealed class RegisterWebhookSubscriptionCommandValidator : AbstractValidator<RegisterWebhookSubscriptionCommand>
{
    public RegisterWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.TargetUrl).NotEmpty().MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Target URL must be an absolute URL.");
    }
}
