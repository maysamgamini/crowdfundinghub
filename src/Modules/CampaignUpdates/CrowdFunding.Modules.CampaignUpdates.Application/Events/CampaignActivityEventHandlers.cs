using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Events;

public sealed class CampaignCreatedActivityHandler : IEventHandler<CampaignCreatedApplicationEvent>
{
    public Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignPublishedActivityHandler : IEventHandler<CampaignPublishedApplicationEvent>
{
    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignCancelledActivityHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ContributionPaymentConfirmedActivityHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignReviewApprovedActivityHandler : IEventHandler<CampaignReviewApprovedApplicationEvent>
{
    public Task Handle(CampaignReviewApprovedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignReviewRejectedActivityHandler : IEventHandler<CampaignReviewRejectedApplicationEvent>
{
    public Task Handle(CampaignReviewRejectedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
