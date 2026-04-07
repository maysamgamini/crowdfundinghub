using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignCommandHandler
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignModerationGateway _campaignModerationGateway;

    public CreateCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IDateTimeProvider dateTimeProvider,
        ICampaignModerationGateway campaignModerationGateway)
    {
        _campaignRepository = campaignRepository;
        _dateTimeProvider = dateTimeProvider;
        _campaignModerationGateway = campaignModerationGateway;
    }

    public async Task<CreateCampaignResult> Handle(
        CreateCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var goalAmount = new Money(command.GoalAmount, command.Currency);

        var campaign = Campaign.Create(
            command.OwnerId,
            command.Title,
            command.Story,
            command.Category,
            goalAmount,
            command.DeadlineUtc,
            _dateTimeProvider.UtcNow);

        await _campaignRepository.AddAsync(campaign, cancellationToken);
        await _campaignModerationGateway.CreateReviewAsync(campaign.Id, cancellationToken);

        return new CreateCampaignResult(campaign.Id);
    }
}
