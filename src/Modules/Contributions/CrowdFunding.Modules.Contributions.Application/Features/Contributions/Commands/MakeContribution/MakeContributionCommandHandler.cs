using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Campaigns.Contracts;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

public sealed class MakeContributionCommandHandler
{
    private readonly ICampaignsModule _campaignsModule;
    private readonly ICurrentUser _currentUser;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;

    public MakeContributionCommandHandler(
        ICampaignsModule campaignsModule,
        ICurrentUser currentUser,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository)
    {
        _campaignsModule = campaignsModule;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
    }

    public async Task<MakeContributionResult> Handle(
        MakeContributionCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to contribute to a campaign.");
        }

        var contribution = Contribution.Create(
            command.CampaignId,
            _currentUser.UserId,
            command.Amount,
            command.Currency,
            _dateTimeProvider.UtcNow);

        await _campaignsModule.AddContributionToCampaignAsync(
            new AddContributionToCampaignCommand(
                contribution.CampaignId,
                contribution.Money.Amount,
                contribution.Money.Currency),
            cancellationToken);

        await _contributionRepository.AddAsync(contribution, cancellationToken);

        return new MakeContributionResult(contribution.Id);
    }
}
