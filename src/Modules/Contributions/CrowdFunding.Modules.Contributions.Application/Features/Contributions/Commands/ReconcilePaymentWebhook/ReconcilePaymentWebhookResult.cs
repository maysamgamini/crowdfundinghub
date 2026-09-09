namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ReconcilePaymentWebhook;

/// <summary>
/// Represents the outcome of reconciling one payment-gateway webhook delivery.
/// </summary>
/// <param name="ContributionId">The reconciled contribution's id, or null if the event was
/// acknowledged but produced no state transition (already processed, or an event type this
/// module doesn't act on).</param>
/// <param name="Status">The contribution's resulting status, or <c>"Ignored"</c>.</param>
public sealed record ReconcilePaymentWebhookResult(Guid? ContributionId, string Status);
