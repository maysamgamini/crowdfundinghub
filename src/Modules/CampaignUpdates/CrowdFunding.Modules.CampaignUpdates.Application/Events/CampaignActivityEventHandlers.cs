using System.Text.Json;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Events;

/// <summary>
/// Replicates a campaign's owner into CampaignUpdates' own local
/// <see cref="ICampaignOwnerCacheRepository"/> (Event-Carried State Transfer), so registering a
/// webhook subscription can enforce ownership without a synchronous cross-module call. See
/// TICKET-023 / TICKET-035.
/// </summary>
public sealed class CampaignCreatedActivityHandler : IEventHandler<CampaignCreatedApplicationEvent>
{
    private readonly ICampaignOwnerCacheRepository _repository;

    public CampaignCreatedActivityHandler(ICampaignOwnerCacheRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken)
        => _repository.UpsertAsync(notification.CampaignId, notification.OwnerId, DateTime.UtcNow, cancellationToken);
}

/// <summary>
/// Handles campaign-published events for the campaign updates module.
/// </summary>
public sealed class CampaignPublishedActivityHandler : IEventHandler<CampaignPublishedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles campaign-cancelled events for the campaign updates module.
/// </summary>
public sealed class CampaignCancelledActivityHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Fans a confirmed pledge out into one durable <see cref="WebhookDeliveryTask"/> per active
/// webhook subscription on the campaign — never an in-memory/synchronous HTTP call from inside
/// this handler, which already runs inside Contributions' own outbox worker and must not become
/// slow or unreliable because a creator's CRM endpoint is. See TICKET-035.
/// </summary>
public sealed class ContributionPaymentConfirmedActivityHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    private const string PledgeConfirmedEventType = "pledge.confirmed";

    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly IWebhookDeliveryTaskRepository _deliveryTaskRepository;
    private readonly ICampaignUpdatesDateTimeProvider _dateTimeProvider;

    public ContributionPaymentConfirmedActivityHandler(
        IWebhookSubscriptionRepository subscriptionRepository,
        IWebhookDeliveryTaskRepository deliveryTaskRepository,
        ICampaignUpdatesDateTimeProvider dateTimeProvider)
    {
        _subscriptionRepository = subscriptionRepository;
        _deliveryTaskRepository = deliveryTaskRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc/>
    public async Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptionRepository.GetActiveByCampaignIdAsync(notification.CampaignId, cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            eventType = PledgeConfirmedEventType,
            contributionId = notification.ContributionId,
            campaignId = notification.CampaignId,
            amount = notification.Amount,
            currency = notification.Currency,
        });

        foreach (var subscription in subscriptions)
        {
            var task = WebhookDeliveryTask.Create(subscription.Id, PledgeConfirmedEventType, payload, _dateTimeProvider.UtcNow);
            await _deliveryTaskRepository.AddAsync(task, cancellationToken);
        }
    }
}

/// <summary>
/// Handles campaign-review-approved events for the campaign updates module.
/// </summary>
public sealed class CampaignReviewApprovedActivityHandler : IEventHandler<CampaignReviewApprovedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignReviewApprovedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles campaign-review-rejected events for the campaign updates module.
/// </summary>
public sealed class CampaignReviewRejectedActivityHandler : IEventHandler<CampaignReviewRejectedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignReviewRejectedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
