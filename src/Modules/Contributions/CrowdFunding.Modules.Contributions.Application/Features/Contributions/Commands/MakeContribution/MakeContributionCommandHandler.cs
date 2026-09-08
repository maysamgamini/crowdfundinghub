using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Contracts.Queries.GetCampaignContributionAvailability;
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
    private readonly ICampaignContributionAvailabilityReader _campaignContributionAvailabilityReader;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;

    public MakeContributionCommandHandler(
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

    public async Task<MakeContributionResult> Handle(MakeContributionCommand command, CancellationToken cancellationToken)
    {
        EnsureCanContribute();

        var campaignAvailability = await _campaignContributionAvailabilityReader.GetCampaignContributionAvailabilityAsync(
            new GetCampaignContributionAvailabilityQuery(command.CampaignId),
            cancellationToken);

        if (!campaignAvailability.Exists)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (!campaignAvailability.CanAcceptContributions)
        {
            throw new InvalidOperationException(
                $"Campaign '{command.CampaignId}' cannot accept contributions while in '{campaignAvailability.Status}' status.");
        }

        // Reject a currency mismatch here, before any Contribution/payment record exists.
        // Left unchecked, this surfaces much later as Money.Add throwing inside
        // AddContributionToCampaignCommandHandler — after the payment was already confirmed —
        // permanently dead-lettering the outbox message with no way to credit the campaign
        // without manual intervention.
        if (!string.Equals(campaignAvailability.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Contribution currency '{command.Currency}' does not match campaign currency '{campaignAvailability.Currency}'.");
        }

        var contribution = Contribution.Create(
            command.CampaignId,
            _currentUser.UserId,
            command.Amount,
            command.Currency,
            _dateTimeProvider.UtcNow);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            await _contributionRepository.AddAsync(contribution, ct);
            return 0;
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
