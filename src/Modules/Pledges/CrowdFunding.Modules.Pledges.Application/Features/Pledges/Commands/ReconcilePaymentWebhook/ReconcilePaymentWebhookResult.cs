namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ReconcilePaymentWebhook;

/// <summary>
/// Represents the outcome of reconciling one payment-gateway webhook delivery.
/// </summary>
/// <param name="PledgeId">The reconciled pledge's id, or null if the event was
/// acknowledged but produced no state transition (already processed, or an event type this
/// module doesn't act on).</param>
/// <param name="Status">The pledge's resulting status, or <c>"Ignored"</c>.</param>
public sealed record ReconcilePaymentWebhookResult(Guid? PledgeId, string Status);
