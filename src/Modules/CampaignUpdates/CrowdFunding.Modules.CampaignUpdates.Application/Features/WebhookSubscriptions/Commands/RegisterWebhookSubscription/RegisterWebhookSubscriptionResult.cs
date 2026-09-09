namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.RegisterWebhookSubscription;

/// <param name="SubscriptionId">The new subscription's id.</param>
/// <param name="CampaignId">The campaign it was registered for.</param>
/// <param name="TargetUrl">The registered delivery target.</param>
/// <param name="SecretKey">Shown exactly once, at registration — the creator must store it
/// themselves to verify the <c>X-CrowdFunding-Signature</c> header on deliveries; it is never
/// re-exposed by any other endpoint.</param>
public sealed record RegisterWebhookSubscriptionResult(Guid SubscriptionId, Guid CampaignId, string TargetUrl, string SecretKey);
