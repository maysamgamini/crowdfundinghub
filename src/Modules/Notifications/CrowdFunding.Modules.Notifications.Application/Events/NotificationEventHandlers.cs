using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentConfirmed;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeRefunded;
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
/// <see cref="PledgeRefundedNotificationHandler"/> below reacts to the per-pledge
/// <c>PledgeRefundedApplicationEvent</c> the refund saga (TICKET-027) already fires once for
/// every backer of a cancelled campaign, which both carries the right recipient and fires exactly
/// once per person who actually needs to hear about it.
/// </summary>
public sealed class CampaignCancelledNotificationHandler : IEventHandler<CampaignCancelledApplicationEvent>
{
    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Sends a backer their pledge confirmation receipt. Runs inside the Pledges module's
/// outbox worker — see <see cref="IEmailNotificationService"/>'s remarks for why a thrown
/// exception here is the intended dual-write defense, not a bug to swallow.
/// </summary>
public sealed class PledgePaymentConfirmedNotificationHandler : IEventHandler<PledgePaymentConfirmedApplicationEvent>
{
    private readonly IEmailNotificationService _emailService;

    public PledgePaymentConfirmedNotificationHandler(IEmailNotificationService emailService)
    {
        _emailService = emailService;
    }

    public Task Handle(PledgePaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
        => _emailService.SendPledgeReceiptAsync(
            notification.BackerId,
            notification.CampaignId,
            notification.PledgeId,
            notification.Amount,
            notification.Currency,
            cancellationToken);
}

/// <summary>
/// The real backer-facing cancellation alert: fires once per refunded pledge (see the
/// comment on <see cref="CampaignCancelledNotificationHandler"/> for why this is the right event
/// to react to instead).
/// </summary>
public sealed class PledgeRefundedNotificationHandler : IEventHandler<PledgeRefundedApplicationEvent>
{
    private readonly IEmailNotificationService _emailService;

    public PledgeRefundedNotificationHandler(IEmailNotificationService emailService)
    {
        _emailService = emailService;
    }

    public Task Handle(PledgeRefundedApplicationEvent notification, CancellationToken cancellationToken)
        => _emailService.SendCampaignCancellationAlertAsync(
            notification.BackerId,
            notification.CampaignId,
            notification.PledgeId,
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
