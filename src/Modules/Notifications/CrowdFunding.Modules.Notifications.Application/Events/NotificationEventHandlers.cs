using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;
using CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Notifications.Application.Events;

public sealed class CampaignPublishedNotificationHandler : IEventHandler<CampaignPublishedApplicationEvent>
{
    public Task Handle(CampaignPublishedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Deliberately not the backer-facing cancellation alert (TICKET-031's acceptance criteria) —
/// the event carries only <c>CampaignId</c>/<c>OwnerId</c>, not the list of affected backers.
/// <see cref="ContributionRefundedNotificationHandler"/> below reacts to the per-contribution
/// <c>ContributionRefundedApplicationEvent</c> the refund saga (TICKET-027) already fires once for
/// every backer of a cancelled campaign, which both carries the right recipient and fires exactly
/// once per person who actually needs to hear about it.
/// </summary>
public sealed class CampaignCancelledNotificationHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Sends a backer their pledge confirmation receipt. Runs inside the Contributions module's
/// outbox worker — see <see cref="IEmailNotificationService"/>'s remarks for why a thrown
/// exception here is the intended dual-write defense, not a bug to swallow.
/// </summary>
public sealed class ContributionPaymentConfirmedNotificationHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    private readonly IEmailNotificationService _emailService;

    public ContributionPaymentConfirmedNotificationHandler(IEmailNotificationService emailService)
    {
        _emailService = emailService;
    }

    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
        => _emailService.SendContributionReceiptAsync(
            notification.ContributorId,
            notification.CampaignId,
            notification.ContributionId,
            notification.Amount,
            notification.Currency,
            cancellationToken);
}

/// <summary>
/// The real backer-facing cancellation alert: fires once per refunded contribution (see the
/// comment on <see cref="CampaignCancelledNotificationHandler"/> for why this is the right event
/// to react to instead).
/// </summary>
public sealed class ContributionRefundedNotificationHandler : IEventHandler<ContributionRefundedApplicationEvent>
{
    private readonly IEmailNotificationService _emailService;

    public ContributionRefundedNotificationHandler(IEmailNotificationService emailService)
    {
        _emailService = emailService;
    }

    public Task Handle(ContributionRefundedApplicationEvent notification, CancellationToken cancellationToken)
        => _emailService.SendCampaignCancellationAlertAsync(
            notification.ContributorId,
            notification.CampaignId,
            notification.ContributionId,
            notification.Amount,
            notification.Currency,
            cancellationToken);
}

public sealed class CampaignReviewApprovedNotificationHandler : IEventHandler<CampaignReviewApprovedApplicationEvent>
{
    public Task Handle(CampaignReviewApprovedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CampaignReviewRejectedNotificationHandler : IEventHandler<CampaignReviewRejectedApplicationEvent>
{
    public Task Handle(CampaignReviewRejectedApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
