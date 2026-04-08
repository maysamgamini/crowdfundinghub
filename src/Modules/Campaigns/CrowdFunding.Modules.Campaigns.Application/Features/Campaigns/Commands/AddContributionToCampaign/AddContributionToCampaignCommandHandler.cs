using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;

public sealed class AddContributionToCampaignCommandHandler : ICommandHandler<AddContributionToCampaignCommand, AddContributionToCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public AddContributionToCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<AddContributionToCampaignResult> Handle(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            campaign.ApplyConfirmedContribution(new Money(command.Amount, command.Currency));
            await _campaignRepository.UpdateAsync(campaign, ct);
            return 0;
        }, cancellationToken);

        return new AddContributionToCampaignResult(campaign.Id, campaign.RaisedAmount.Amount, campaign.RaisedAmount.Currency);
    }
}
