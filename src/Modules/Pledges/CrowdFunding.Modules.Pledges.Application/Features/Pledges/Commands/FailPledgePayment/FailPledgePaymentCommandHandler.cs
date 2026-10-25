using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;

/// <summary>
/// Handles Fail Pledge Payment command requests.
/// </summary>
public sealed class FailPledgePaymentCommandHandler : ICommandHandler<FailPledgePaymentCommand, FailPledgePaymentResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;
    private readonly IPledgeRepository _pledgeRepository;
    private readonly IPledgeTransactionExecutor _transactionExecutor;

    public FailPledgePaymentCommandHandler(
        ICurrentUser currentUser,
        IPledgeDateTimeProvider dateTimeProvider,
        IPledgeRepository pledgeRepository,
        IPledgeTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _pledgeRepository = pledgeRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<FailPledgePaymentResult> Handle(FailPledgePaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var pledge = await _pledgeRepository.GetByIdAsync(command.PledgeId, cancellationToken);
        if (pledge is null || pledge.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Pledge '{command.PledgeId}' was not found for campaign '{command.CampaignId}'.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            pledge.FailPayment(command.FailureReason, _dateTimeProvider.UtcNow);
            await _pledgeRepository.UpdateAsync(pledge, ct);
        }, cancellationToken);

        return new FailPledgePaymentResult(pledge.Id, pledge.Status.ToString(), pledge.FailureReason!);
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
