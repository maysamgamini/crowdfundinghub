using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;

/// <summary>
/// The refund compensation saga (event-driven choreography, no distributed 2PC transaction):
/// when Campaigns marks a campaign Failed or Cancelled and publishes that as an application
/// event via its own outbox, Pledges reacts asynchronously by refunding every backer who
/// had already had their payment confirmed — Campaigns never opens a transaction against
/// Pledges' database to do this itself.
///
/// Naturally idempotent: <see cref="IPledgeRepository.GetSucceededByCampaignIdAsync"/> only
/// ever returns pledges still in <c>Succeeded</c> status, so re-delivering either
/// triggering event a second time finds nothing left to refund and is a safe no-op.
/// </summary>
public sealed class CampaignTerminationRefundHandler :
    IEventHandler<CampaignFailedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>
{
    private readonly IPledgeRepository _pledgeRepository;
    private readonly PledgeRefunder _refunder;

    public CampaignTerminationRefundHandler(
        IPledgeRepository pledgeRepository,
        PledgeRefunder refunder)
    {
        _pledgeRepository = pledgeRepository;
        _refunder = refunder;
    }

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededPledgesAsync(notification.CampaignId, cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededPledgesAsync(notification.CampaignId, cancellationToken);

    private async Task RefundAllSucceededPledgesAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        const int batchSize = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _pledgeRepository.GetSucceededBatchByCampaignIdAsync(campaignId, batchSize, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            // One refund at a time: each is a call to the payment provider followed by a short transaction.
            // If one fails the event is retried, and the pledges already refunded no longer show up as
            // Succeeded, so the retry carries on with the rest.
            foreach (var pledge in batch)
            {
                await _refunder.RefundAsync(pledge, cancellationToken);
            }
        }
    }
}
