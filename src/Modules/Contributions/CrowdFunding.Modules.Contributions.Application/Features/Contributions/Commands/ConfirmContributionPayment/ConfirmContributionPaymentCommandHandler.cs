using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;
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
    private readonly ICampaignContributionAvailabilityReader _campaignContributionAvailabilityReader;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public ConfirmContributionPaymentCommandHandler(
        ICampaignContributionAvailabilityReader campaignContributionAvailabilityReader,
        ICurrentUser currentUser,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor)
    {
        _campaignContributionAvailabilityReader = campaignContributionAvailabilityReader;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<ConfirmContributionPaymentResult> Handle(ConfirmContributionPaymentCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManagePayments();

        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);
        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        var campaignAvailability = await _campaignContributionAvailabilityReader.GetCampaignContributionAvailabilityAsync(
            new GetCampaignContributionAvailabilityQuery(command.CampaignId),
            cancellationToken);

        if (!campaignAvailability.Exists)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!string.Equals(campaignAvailability.Status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Contribution payments can only be confirmed while the campaign is published.");
        }

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            contribution.ConfirmPayment(command.PaymentReference, _dateTimeProvider.UtcNow);
            await _contributionRepository.UpdateAsync(contribution, ct);
            return 0;
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
