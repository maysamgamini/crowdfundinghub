using FluentValidation;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;

/// <summary>
/// Validates Reconcile Payment Webhook Command instances before they reach the handler.
/// </summary>
public sealed class ReconcilePaymentWebhookCommandValidator : AbstractValidator<ReconcilePaymentWebhookCommand>
{
    public ReconcilePaymentWebhookCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PaymentIntentId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(100);
    }
}
