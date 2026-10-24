using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;

/// <summary>
/// The refund compensation saga (event-driven choreography, no distributed 2PC transaction):
/// when Campaigns marks a campaign Failed or Cancelled and publishes that as an application
/// event via its own outbox, Contributions reacts asynchronously by refunding every backer who
/// had already had their payment confirmed — Campaigns never opens a transaction against
/// Contributions' database to do this itself.
///
/// Naturally idempotent: <see cref="IContributionRepository.GetSucceededByCampaignIdAsync"/> only
/// ever returns contributions still in <c>Succeeded</c> status, so re-delivering either
/// triggering event a second time finds nothing left to refund and is a safe no-op.
/// </summary>
public sealed class CampaignTerminationRefundHandler :
    IEventHandler<CampaignFailedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>
{
    private readonly IContributionRepository _contributionRepository;
    private readonly ContributionRefunder _refunder;

    public CampaignTerminationRefundHandler(
        IContributionRepository contributionRepository,
        ContributionRefunder refunder)
    {
        _contributionRepository = contributionRepository;
        _refunder = refunder;
    }

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededContributionsAsync(notification.CampaignId, cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededContributionsAsync(notification.CampaignId, cancellationToken);

    private async Task RefundAllSucceededContributionsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        const int batchSize = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _contributionRepository.GetSucceededBatchByCampaignIdAsync(campaignId, batchSize, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            // One refund at a time: each is a call to the payment provider followed by a short transaction.
            // If one fails the event is retried, and the contributions already refunded no longer show up as
            // Succeeded, so the retry carries on with the rest.
            foreach (var contribution in batch)
            {
                await _refunder.RefundAsync(contribution, cancellationToken);
            }
        }
    }
}
