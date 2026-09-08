using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Notifications.Application.Events;

/// <summary>
/// Handles campaign-published events for notification workflows.
/// </summary>
public sealed class CampaignPublishedNotificationHandler : IEventHandler<CampaignPublishedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles campaign-cancelled events for notification workflows.
/// </summary>
public sealed class CampaignCancelledNotificationHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles contribution-payment-confirmed events for notification workflows.
/// </summary>
public sealed class ContributionPaymentConfirmedNotificationHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles campaign-review-approved events for notification workflows.
/// </summary>
public sealed class CampaignReviewApprovedNotificationHandler : IEventHandler<CampaignReviewApprovedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignReviewApprovedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handles campaign-review-rejected events for notification workflows.
/// </summary>
public sealed class CampaignReviewRejectedNotificationHandler : IEventHandler<CampaignReviewRejectedApplicationEvent>
{
    /// <inheritdoc/>
    public Task Handle(CampaignReviewRejectedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
