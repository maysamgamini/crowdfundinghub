using System.Text.Json;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Inbox;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;
using CrowdFunding.Modules.CampaignUpdates.Contracts.Webhooks;
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
/// <remarks>
/// Appending delivery tasks is not naturally idempotent: the outbox retries the whole event when
/// any other consumer of it fails, and every retry would enqueue (and later POST) a duplicate
/// webhook. The fan-out therefore runs through <see cref="ICampaignUpdatesIdempotentConsumer"/>,
/// keyed by the event's <c>EventId</c>, so the tasks and the processed-event record commit together
/// exactly once.
/// </remarks>
public sealed class ContributionPaymentConfirmedActivityHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    // The payload is the public webhook contract (camelCase JSON), serialized once here and
    // signed and sent verbatim by the dispatcher.
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly IWebhookDeliveryTaskRepository _deliveryTaskRepository;
    private readonly ICampaignUpdatesDateTimeProvider _dateTimeProvider;
    private readonly ICampaignUpdatesIdempotentConsumer _idempotentConsumer;

    public ContributionPaymentConfirmedActivityHandler(
        IWebhookSubscriptionRepository subscriptionRepository,
        IWebhookDeliveryTaskRepository deliveryTaskRepository,
        ICampaignUpdatesDateTimeProvider dateTimeProvider,
        ICampaignUpdatesIdempotentConsumer idempotentConsumer)
    {
        _subscriptionRepository = subscriptionRepository;
        _deliveryTaskRepository = deliveryTaskRepository;
        _dateTimeProvider = dateTimeProvider;
        _idempotentConsumer = idempotentConsumer;
    }

    /// <inheritdoc/>
    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
        => _idempotentConsumer.ExecuteOnceAsync(
            notification.EventId.ToString(),
            nameof(ContributionPaymentConfirmedActivityHandler),
            ct => EnqueueDeliveriesAsync(notification, ct),
            cancellationToken);

    private async Task EnqueueDeliveriesAsync(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptionRepository.GetActiveByCampaignIdAsync(notification.CampaignId, cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(
            new PledgeConfirmedWebhookPayload(
                WebhookEventTypes.PledgeConfirmed,
                notification.ContributionId,
                notification.CampaignId,
                notification.Amount,
                notification.Currency),
            PayloadSerializerOptions);

        foreach (var subscription in subscriptions)
        {
            var task = WebhookDeliveryTask.Create(subscription.Id, WebhookEventTypes.PledgeConfirmed, payload, _dateTimeProvider.UtcNow);
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
