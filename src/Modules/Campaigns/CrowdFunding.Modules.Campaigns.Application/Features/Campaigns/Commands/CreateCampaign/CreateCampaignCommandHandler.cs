using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignCommandHandler : ICommandHandler<CreateCampaignCommand, CreateCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public CreateCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<CreateCampaignResult> Handle(CreateCampaignCommand command, CancellationToken cancellationToken)
    {
        EnsureCanCreateCampaign();

        var goalAmount = new Money(command.GoalAmount, command.Currency);
        var campaign = Campaign.Create(
            _currentUser.UserId,
            command.Title,
            command.Story,
            command.Category,
            goalAmount,
            command.DeadlineUtc,
            _dateTimeProvider.UtcNow);

        await _transactionExecutor.ExecuteAsync(async ct =>
        {
            await _campaignRepository.AddAsync(campaign, ct);
            return 0;
        }, cancellationToken);

        return new CreateCampaignResult(campaign.Id);
    }

    private void EnsureCanCreateCampaign()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to create a campaign.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.CampaignsCreate))
        {
            throw new ForbiddenAccessException("The current user does not have permission to create campaigns.");
        }
    }
}