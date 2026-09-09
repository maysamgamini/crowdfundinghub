using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

/// <summary>
/// Handles Make Contribution command requests.
/// </summary>
public sealed class MakeContributionCommandHandler : ICommandHandler<MakeContributionCommand, MakeContributionResult>
{
    private readonly IActiveCampaignCacheRepository _activeCampaignCacheRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public MakeContributionCommandHandler(
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
    public async Task<MakeContributionResult> Handle(MakeContributionCommand command, CancellationToken cancellationToken)
    {
        EnsureCanContribute();

        // Reads Contributions' own locally replicated campaign snapshot rather than calling back
        // into Campaigns synchronously (TICKET-023) — see ReplicatedCampaignEventHandlers.
        var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken);

        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!campaign.IsActive || campaign.DeadlineUtc <= _dateTimeProvider.UtcNow)
        {
            throw new InvalidOperationException(
                $"Campaign '{command.CampaignId}' cannot accept contributions — it is not active or has passed its deadline.");
        }

        // Reject a currency mismatch here, before any Contribution/payment record exists.
        // Left unchecked, this surfaces much later as Money.Add throwing inside
        // AddContributionToCampaignCommandHandler — after the payment was already confirmed —
        // permanently dead-lettering the outbox message with no way to credit the campaign
        // without manual intervention.
        if (!string.Equals(campaign.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Contribution currency '{command.Currency}' does not match campaign currency '{campaign.Currency}'.");
        }

        var contribution = Contribution.Create(
            command.CampaignId,
            _currentUser.UserId,
            command.Amount,
            command.Currency,
            _dateTimeProvider.UtcNow,
            command.RewardTierReservationId);

        // In a real integration this id/reference comes back from creating a PaymentIntent (or
        // equivalent) with the external gateway before the client is redirected to complete
        // authentication (3DS) — see TICKET-033. No live gateway call is made here; this is the
        // correlation key an inbound webhook reconciles against in ReconcilePaymentWebhookCommandHandler.
        contribution.AttachPaymentIntent($"pi_mock_{contribution.Id:N}", "Mock");

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            await _contributionRepository.AddAsync(contribution, ct);
        }, cancellationToken);

        return new MakeContributionResult(contribution.Id, contribution.Status.ToString());
    }

    private void EnsureCanContribute()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to contribute to a campaign.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.CampaignsContribute))
        {
            throw new ForbiddenAccessException("The current user does not have permission to contribute to campaigns.");
        }
    }
}
