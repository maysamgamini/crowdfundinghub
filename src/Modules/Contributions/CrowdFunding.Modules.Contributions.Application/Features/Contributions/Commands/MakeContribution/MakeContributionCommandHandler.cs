using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

public sealed class MakeContributionCommandHandler
{
    private readonly ICampaignContributionGateway _campaignContributionGateway;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;

    public MakeContributionCommandHandler(
        ICampaignContributionGateway campaignContributionGateway,
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository)
    {
        _campaignContributionGateway = campaignContributionGateway;
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
    }

    public async Task<MakeContributionResult> Handle(
        MakeContributionCommand command,
        CancellationToken cancellationToken)
    {
        var contribution = Contribution.Create(
            command.CampaignId,
            command.ContributorId,
            command.Amount,
            command.Currency,
            _dateTimeProvider.UtcNow);

        await _campaignContributionGateway.ApplyContributionAsync(
            contribution.CampaignId,
            contribution.Money.Amount,
            contribution.Money.Currency,
            cancellationToken);

        await _contributionRepository.AddAsync(contribution, cancellationToken);

        return new MakeContributionResult(contribution.Id);
    }
}
