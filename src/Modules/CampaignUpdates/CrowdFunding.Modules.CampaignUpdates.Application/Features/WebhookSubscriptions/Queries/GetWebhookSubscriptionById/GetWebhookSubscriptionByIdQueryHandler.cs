using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.GetWebhookSubscriptionById;

/// <summary>
/// Handles Get Webhook Subscription By Id query requests. Backs the <c>Location</c> header
/// returned by <c>POST /api/campaigns/{campaignId}/webhook-subscriptions</c> — TICKET-047.
/// </summary>
public sealed class GetWebhookSubscriptionByIdQueryHandler
    : IQueryHandler<GetWebhookSubscriptionByIdQuery, GetWebhookSubscriptionByIdResult>
{
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly ICampaignOwnerCacheRepository _campaignOwnerCacheRepository;
    private readonly ICurrentUser _currentUser;

    public GetWebhookSubscriptionByIdQueryHandler(
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        ICampaignOwnerCacheRepository campaignOwnerCacheRepository,
        ICurrentUser currentUser)
    {
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _campaignOwnerCacheRepository = campaignOwnerCacheRepository;
        _currentUser = currentUser;
    }

    public async Task<GetWebhookSubscriptionByIdResult> Handle(GetWebhookSubscriptionByIdQuery query, CancellationToken cancellationToken)
    {
        var subscription = await _webhookSubscriptionRepository.GetByIdAsync(query.SubscriptionId, cancellationToken);

        if (subscription is null || subscription.CampaignId != query.CampaignId)
        {
            throw new KeyNotFoundException($"Webhook subscription '{query.SubscriptionId}' was not found for campaign '{query.CampaignId}'.");
        }

        await EnsureOwnerAsync(query.CampaignId, cancellationToken);

        return new GetWebhookSubscriptionByIdResult(
            subscription.Id,
            subscription.CampaignId,
            subscription.TargetUrl,
            subscription.IsActive,
            subscription.ConsecutiveFailureCount,
            subscription.CreatedAtUtc);
    }

    private async Task EnsureOwnerAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to view webhook subscriptions.");
        }

        var campaign = await _campaignOwnerCacheRepository.GetAsync(campaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{campaignId}' was not found.");
        }

        if (campaign.OwnerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only a campaign's owner may view its webhook subscriptions.");
        }
    }
}
