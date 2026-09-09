using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Queries.ListWebhookSubscriptionsByCampaign;

/// <summary>
/// Handles List Webhook Subscriptions By Campaign query requests — lets a creator see and manage
/// their registered webhook targets. See TICKET-047.
/// </summary>
public sealed class ListWebhookSubscriptionsByCampaignQueryHandler
    : IQueryHandler<ListWebhookSubscriptionsByCampaignQuery, ListWebhookSubscriptionsByCampaignResult>
{
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly ICampaignOwnerCacheRepository _campaignOwnerCacheRepository;
    private readonly ICurrentUser _currentUser;

    public ListWebhookSubscriptionsByCampaignQueryHandler(
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        ICampaignOwnerCacheRepository campaignOwnerCacheRepository,
        ICurrentUser currentUser)
    {
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _campaignOwnerCacheRepository = campaignOwnerCacheRepository;
        _currentUser = currentUser;
    }

    public async Task<ListWebhookSubscriptionsByCampaignResult> Handle(
        ListWebhookSubscriptionsByCampaignQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to view webhook subscriptions.");
        }

        var campaign = await _campaignOwnerCacheRepository.GetAsync(query.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{query.CampaignId}' was not found.");
        }

        if (campaign.OwnerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only a campaign's owner may view its webhook subscriptions.");
        }

        var subscriptions = await _webhookSubscriptionRepository.GetByCampaignIdAsync(query.CampaignId, cancellationToken);

        var summaries = subscriptions
            .Select(subscription => new WebhookSubscriptionSummary(
                subscription.Id,
                subscription.CampaignId,
                subscription.TargetUrl,
                subscription.IsActive,
                subscription.ConsecutiveFailureCount,
                subscription.CreatedAtUtc))
            .ToList();

        return new ListWebhookSubscriptionsByCampaignResult(summaries);
    }
}
