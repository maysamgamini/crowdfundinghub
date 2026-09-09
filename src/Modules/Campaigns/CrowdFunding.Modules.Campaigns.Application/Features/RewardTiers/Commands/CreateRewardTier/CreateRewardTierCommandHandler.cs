using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;

/// <summary>
/// Lets a campaign's creator define a limited-quantity reward perk tier. See TICKET-034.
/// </summary>
public sealed class CreateRewardTierCommandHandler : ICommandHandler<CreateRewardTierCommand, CreateRewardTierResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public CreateRewardTierCommandHandler(
        ICampaignRepository campaignRepository,
        IRewardTierRepository rewardTierRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _rewardTierRepository = rewardTierRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<CreateRewardTierResult> Handle(CreateRewardTierCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to create a reward tier.");
        }

        var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);
        if (campaign is null)
        {
            throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
        }

        if (campaign.OwnerId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("Only a campaign's owner may define reward tiers for it.");
        }

        var rewardTier = RewardTier.Create(
            command.CampaignId,
            command.Title,
            command.Description,
            new Money(command.MinimumPledgeAmount, command.Currency),
            command.TotalCapacity,
            _dateTimeProvider.UtcNow);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            await _rewardTierRepository.AddAsync(rewardTier, ct);
        }, cancellationToken);

        return new CreateRewardTierResult(rewardTier.Id, rewardTier.CampaignId, rewardTier.Title, rewardTier.TotalCapacity, rewardTier.AvailableCount);
    }
}
