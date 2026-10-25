using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmPledgeSetup;

/// <summary>
/// The card was saved: the pledge becomes <c>Active</c>, which is when it starts to count toward the campaign.
/// Nothing is charged. In production this is the payment provider's "setup succeeded" webhook; here it is also an
/// endpoint for people who hold the payments-manage permission, like the confirm-payment endpoint of the other model.
/// </summary>
public sealed class ConfirmPledgeSetupCommandHandler : ICommandHandler<ConfirmPledgeSetupCommand, ConfirmPledgeSetupResult>
{
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public ConfirmPledgeSetupCommandHandler(
        IActiveCampaignCacheRepository activeCampaignCacheRepository,
        ICurrentUser currentUser,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor)
    {
        _activeCampaignCacheRepository = activeCampaignCacheRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<ConfirmPledgeSetupResult> Handle(ConfirmPledgeSetupCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);
        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        // The provider's news can arrive twice; the second one finds the pledge already active and changes nothing.
        if (contribution.Status == ContributionStatus.Active)
        {
            return new ConfirmPledgeSetupResult(contribution.Id, contribution.Status.ToString());
        }

        var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken)
            ?? throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

        if (!campaign.IsActive)
        {
            throw new InvalidOperationException("A card can only be saved for a pledge while the campaign is published.");
        }

        try
        {
            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                var activation = contribution.ActivatePledge(command.PaymentMethodReference, _dateTimeProvider.UtcNow);
                await _contributionRepository.AddActivationAsync(activation, ct);
                await _contributionRepository.UpdateAsync(contribution, ct);
            }, cancellationToken);
        }
        catch (PledgeAlreadyActivatedException)
        {
            // Two copies of the provider's news got past the status check together and one lost the race at the
            // activation index. The winner's work stands: report the pledge as it is now (the executor already dropped
            // the loser's tracked state, so this read is fresh). A lost race at the row's version stays a retryable conflict.
            var current = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);
            if (current?.Status != ContributionStatus.Active)
            {
                throw;
            }

            return new ConfirmPledgeSetupResult(current.Id, current.Status.ToString());
        }

        return new ConfirmPledgeSetupResult(contribution.Id, contribution.Status.ToString());
    }

    private void EnsureCanManagePayments()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to manage contribution payments.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.ContributionsPaymentsManage))
        {
            throw new ForbiddenAccessException("The current user does not have permission to manage contribution payments.");
        }
    }
}
