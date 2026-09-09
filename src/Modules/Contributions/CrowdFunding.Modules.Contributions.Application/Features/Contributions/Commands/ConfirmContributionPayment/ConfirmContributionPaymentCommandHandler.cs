using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

/// <summary>
/// Handles Confirm Contribution Payment command requests.
/// </summary>
public sealed class ConfirmContributionPaymentCommandHandler : ICommandHandler<ConfirmContributionPaymentCommand, ConfirmContributionPaymentResult>
{
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public ConfirmContributionPaymentCommandHandler(
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

    /// <inheritdoc/>
    public async Task<ConfirmContributionPaymentResult> Handle(ConfirmContributionPaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);
        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        // Reads Contributions' own locally replicated campaign snapshot rather than calling back
        // into Campaigns synchronously (TICKET-023) — see ReplicatedCampaignEventHandlers.
        var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!campaign.IsActive)
        {
            throw new InvalidOperationException("Contribution payments can only be confirmed while the campaign is published.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            contribution.ConfirmPayment(command.PaymentReference, _dateTimeProvider.UtcNow);
            await _contributionRepository.UpdateAsync(contribution, ct);
        }, cancellationToken);

        return new ConfirmContributionPaymentResult(contribution.Id, contribution.Status.ToString(), contribution.PaymentReference!);
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
