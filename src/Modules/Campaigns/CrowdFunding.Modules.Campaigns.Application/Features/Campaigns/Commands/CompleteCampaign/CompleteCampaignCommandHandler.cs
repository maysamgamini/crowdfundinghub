using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CompleteCampaign;

/// <summary>
/// Handles Complete Campaign command requests.
/// </summary>
public sealed class CompleteCampaignCommandHandler : ICommandHandler<CompleteCampaignCommand, CompleteCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignTransactionExecutor _transactionExecutor;
    private readonly IContributionLedger _contributionLedger;
    private readonly ILogger<CompleteCampaignCommandHandler> _logger;

    public CompleteCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IDateTimeProvider dateTimeProvider,
        ICampaignTransactionExecutor transactionExecutor,
        IContributionLedger contributionLedger,
        ILogger<CompleteCampaignCommandHandler> logger)
    {
        _campaignRepository = campaignRepository;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
        _contributionLedger = contributionLedger;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CompleteCampaignResult> Handle(CompleteCampaignCommand command, CancellationToken cancellationToken)
    {
        var lockKey = AdvisoryLockKey.FromGuid(command.CampaignId);

        return await _transactionExecutor.ExecuteAsync(lockKey, async ct =>
        {
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                           ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            // Decide on the ledger's exact total, not on the stored counter (see CampaignLedgerReconciliation).
            await CampaignLedgerReconciliation.ReconcileAsync(campaign, _contributionLedger, _logger, ct);

            campaign.CompleteSuccessfully(_dateTimeProvider.UtcNow);
            await _campaignRepository.UpdateAsync(campaign, ct);

            return new CompleteCampaignResult(campaign.Id, campaign.Status.ToString());
        }, cancellationToken);
    }
}
