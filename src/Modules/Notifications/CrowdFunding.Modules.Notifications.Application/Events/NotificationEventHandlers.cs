using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Notifications.Application.Events;

public sealed class CampaignPublishedNotificationHandler : IEventHandler<CampaignPublishedApplicationEvent>
{
    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignCancelledNotificationHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ContributionPaymentConfirmedNotificationHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignReviewApprovedNotificationHandler : IEventHandler<CampaignReviewApprovedApplicationEvent>
{
    public Task Handle(CampaignReviewApprovedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignReviewRejectedNotificationHandler : IEventHandler<CampaignReviewRejectedApplicationEvent>
{
    public Task Handle(CampaignReviewRejectedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
