namespace CrowdFunding.API.Contracts.CampaignUpdates;

/// <summary>
/// Represents the HTTP request payload for registering a creator webhook subscription.
/// </summary>
/// <param name="TargetUrl">The public HTTPS endpoint to deliver pledge activity to.</param>
public sealed record RegisterWebhookSubscriptionRequest(string TargetUrl);
