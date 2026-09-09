namespace CrowdFunding.API.Contracts.CampaignUpdates;

/// <summary>
/// Represents the HTTP response payload returned upon registering a webhook subscription.
/// </summary>
/// <param name="SubscriptionId">The new subscription's id.</param>
/// <param name="CampaignId">The campaign it was registered for.</param>
/// <param name="TargetUrl">The registered delivery target.</param>
/// <param name="SecretKey">Shown exactly once — store it now to verify the
/// <c>X-CrowdFunding-Signature</c> header on future deliveries.</param>
public sealed record RegisterWebhookSubscriptionResponse(Guid SubscriptionId, Guid CampaignId, string TargetUrl, string SecretKey);
