using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns;

/// <summary>
/// Before a campaign is decided, its stored raised amount is checked against the ledger. The counter is a
/// snapshot kept for fast reads; the ledger is the record. A decision made on a drifted counter would fund or
/// fail a campaign wrongly, so the decision uses the ledger's exact total, and any disagreement is logged as an
/// error so a person looks at how the two came apart.
/// </summary>
internal static class CampaignLedgerReconciliation
{
    public static async Task ReconcileAsync(
        Campaign campaign,
        IContributionLedger ledger,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var exact = await ledger.SumAsync(campaign.Id, cancellationToken);

        if (exact < 0)
        {
            throw new InvalidOperationException(
                $"The ledger of campaign '{campaign.Id}' sums to {exact}, which is below zero. Refusing to decide on a broken ledger.");
        }

        var storedAmount = campaign.RaisedAmount.Amount;

        if (campaign.ReconcileRaisedAmount(new Money(exact, campaign.RaisedAmount.Currency)))
        {
            logger.LogError(
                "Campaign {CampaignId}: the stored raised amount {Stored} disagreed with the ledger total {Exact}. The decision uses the ledger total.",
                campaign.Id, storedAmount, exact);
        }
    }
}
