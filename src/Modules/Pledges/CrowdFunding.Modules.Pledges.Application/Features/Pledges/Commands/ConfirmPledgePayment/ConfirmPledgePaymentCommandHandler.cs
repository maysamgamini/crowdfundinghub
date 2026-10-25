using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;

/// <summary>
/// Handles Confirm Pledge Payment command requests.
/// </summary>
public sealed class ConfirmPledgePaymentCommandHandler : ICommandHandler<ConfirmPledgePaymentCommand, ConfirmPledgePaymentResult>
{
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPledgeRepository _pledgeRepository;
    private readonly IPledgeTransactionExecutor _transactionExecutor;

    public ConfirmPledgePaymentCommandHandler(
        IActiveCampaignCacheRepository activeCampaignCacheRepository,
        ICurrentUser currentUser,
        IPledgeDateTimeProvider dateTimeProvider,
        IPledgeRepository pledgeRepository,
        IPledgeTransactionExecutor transactionExecutor)
    {
        _activeCampaignCacheRepository = activeCampaignCacheRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _pledgeRepository = pledgeRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<ConfirmPledgePaymentResult> Handle(ConfirmPledgePaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var pledge = await _pledgeRepository.GetByIdAsync(command.PledgeId, cancellationToken);
        if (pledge is null || pledge.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Pledge '{command.PledgeId}' was not found for campaign '{command.CampaignId}'.");
        }

        // TICKET-033 "Out-of-Order Safety": a payment gateway webhook can reconcile this exact
        // pledge to Succeeded before the client's own confirmation call arrives (the
        // webhook can beat the browser redirect back to the app). Treating that race as success
        // rather than throwing is what makes the client-facing confirm path safe to call
        // regardless of which side — webhook or client — got there first.
        if (pledge.Status == PledgeStatus.Succeeded)
        {
            return new ConfirmPledgePaymentResult(pledge.Id, pledge.Status.ToString(), pledge.PaymentReference!);
        }

        // Reads Pledges' own locally replicated campaign snapshot rather than calling back
        // into Campaigns synchronously (TICKET-023) — see ReplicatedCampaignEventHandlers.
        var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!campaign.IsActive)
        {
            throw new InvalidOperationException("Pledge payments can only be confirmed while the campaign is published.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            pledge.ConfirmPayment(command.PaymentReference, _dateTimeProvider.UtcNow);
            await _pledgeRepository.UpdateAsync(pledge, ct);
        }, cancellationToken);

        return new ConfirmPledgePaymentResult(pledge.Id, pledge.Status.ToString(), pledge.PaymentReference!);
    }

    private void EnsureCanManagePayments()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to manage pledge payments.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.PledgesPaymentsManage))
        {
            throw new ForbiddenAccessException("The current user does not have permission to manage pledge payments.");
        }
    }
}
