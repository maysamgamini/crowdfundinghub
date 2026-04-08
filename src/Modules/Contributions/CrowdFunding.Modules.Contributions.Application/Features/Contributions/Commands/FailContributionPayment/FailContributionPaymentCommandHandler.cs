using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

public sealed class FailContributionPaymentCommandHandler : ICommandHandler<FailContributionPaymentCommand, FailContributionPaymentResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public FailContributionPaymentCommandHandler(
        ICurrentUser currentUser,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<FailContributionPaymentResult> Handle(FailContributionPaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);
        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            contribution.FailPayment(command.FailureReason, _dateTimeProvider.UtcNow);
            await _contributionRepository.UpdateAsync(contribution, ct);
            return 0;
        }, cancellationToken);

        return new FailContributionPaymentResult(contribution.Id, contribution.Status.ToString(), contribution.FailureReason!);
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