using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Features.WebhookSubscriptions.Commands.DeleteWebhookSubscription;

/// <summary>
/// Handles Delete Webhook Subscription command requests. See TICKET-047.
/// </summary>
public sealed class DeleteWebhookSubscriptionCommandHandler : ICommandHandler<DeleteWebhookSubscriptionCommand, DeleteWebhookSubscriptionResult>
{
    private readonly IWebhookSubscriptionRepository _webhookSubscriptionRepository;
    private readonly ICampaignOwnerCacheRepository _campaignOwnerCacheRepository;
    private readonly ICurrentUser _currentUser;

    public DeleteWebhookSubscriptionCommandHandler(
        IWebhookSubscriptionRepository webhookSubscriptionRepository,
        ICampaignOwnerCacheRepository campaignOwnerCacheRepository,
        ICurrentUser currentUser)
    {
        _webhookSubscriptionRepository = webhookSubscriptionRepository;
        _campaignOwnerCacheRepository = campaignOwnerCacheRepository;
        _currentUser = currentUser;
    }

    public async Task<DeleteWebhookSubscriptionResult> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to delete a webhook subscription.");
        }

        var campaign = await _campaignOwnerCacheRepository.GetAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (campaign.OwnerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only a campaign's owner may delete its webhook subscriptions.");
        }

        var subscription = await _webhookSubscriptionRepository.GetByIdAsync(command.SubscriptionId, cancellationToken);

        if (subscription is null || subscription.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Webhook subscription '{command.SubscriptionId}' was not found for campaign '{command.CampaignId}'.");
        }

        subscription.Deactivate();
        await _webhookSubscriptionRepository.UpdateAsync(subscription, cancellationToken);

        return new DeleteWebhookSubscriptionResult();
    }
}
