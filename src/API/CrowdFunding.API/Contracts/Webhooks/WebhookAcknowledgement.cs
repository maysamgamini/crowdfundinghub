namespace CrowdFunding.API.Contracts.Webhooks;

/// <summary>
/// The body returned to an inbound webhook sender once the callback was accepted.
/// </summary>
/// <param name="Status">What happened: the resulting state, or <c>Ignored</c> for a duplicate delivery.</param>
public sealed record WebhookAcknowledgement(string Status);
