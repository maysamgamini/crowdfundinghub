using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Moderation.Contracts;
using CrowdFunding.Modules.Moderation.Contracts.Commands.CreateCampaignReview;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IModerationModule _moderationModule;

    public CreateCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        IModerationModule moderationModule)
    {
        _campaignRepository = campaignRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _moderationModule = moderationModule;
    }

    public async Task<CreateCampaignResult> Handle(
        CreateCampaignCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to create a campaign.");
        }

        var goalAmount = new Money(command.GoalAmount, command.Currency);

        var campaign = Campaign.Create(
            _currentUser.UserId,
            command.Title,
            command.Story,
            command.Category,
            goalAmount,
            command.DeadlineUtc,
            _dateTimeProvider.UtcNow);

        await _campaignRepository.AddAsync(campaign, cancellationToken);
        await _moderationModule.CreateCampaignReviewAsync(
            new CreateCampaignReviewCommand(campaign.Id),
            cancellationToken);

        return new CreateCampaignResult(campaign.Id);
    }
}
