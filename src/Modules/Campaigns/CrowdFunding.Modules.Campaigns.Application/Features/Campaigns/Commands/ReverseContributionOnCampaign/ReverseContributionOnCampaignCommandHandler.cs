using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.ReverseContributionOnCampaign;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.ReverseContributionOnCampaign;

/// <summary>
/// Books a refund against a campaign: a negative ledger entry, and the raised balance goes down, in the same
/// per-campaign transaction that credits pledges and makes the deadline decision.
/// </summary>
/// <remarks>
/// The order of events is not guaranteed. A refund can be processed before the credit it reverses. The refund
/// entry is written either way; if the pledge has not been counted yet, the balance is left alone, and when the
/// credit arrives it sees the refund and counts nothing (the two entries cancel in the ledger). Whichever
/// order the events come in, the ledger sums to the same number.
/// </remarks>
public sealed class ReverseContributionOnCampaignCommandHandler : ICommandHandler<ReverseContributionOnCampaignCommand, ReverseContributionOnCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IContributionLedger _contributionLedger;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public ReverseContributionOnCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IContributionLedger contributionLedger,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _contributionLedger = contributionLedger;
        _transactionExecutor = transactionExecutor;
    }

    public Task<ReverseContributionOnCampaignResult> Handle(ReverseContributionOnCampaignCommand command, CancellationToken cancellationToken)
    {
        var lockKey = AdvisoryLockKey.FromGuid(command.CampaignId);

        return _transactionExecutor.ExecuteAsync(lockKey, async ct =>
        {
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                           ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            var recorded = await _contributionLedger.TryRecordRefundAsync(
                command.CampaignId, command.ContributionId, command.Amount, command.Currency, ct);

            var reversed = false;

            if (recorded && await _contributionLedger.HasPledgeAsync(command.ContributionId, ct))
            {
                campaign.ReverseConfirmedContribution(new Money(command.Amount, command.Currency));
                await _campaignRepository.UpdateAsync(campaign, ct);
                reversed = true;
            }

            return new ReverseContributionOnCampaignResult(campaign.Id, campaign.RaisedAmount.Amount, campaign.RaisedAmount.Currency, reversed);
        }, cancellationToken);
    }
}
