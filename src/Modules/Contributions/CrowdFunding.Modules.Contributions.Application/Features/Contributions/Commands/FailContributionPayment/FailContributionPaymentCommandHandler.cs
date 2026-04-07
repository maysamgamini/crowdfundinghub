using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

public sealed class FailContributionPaymentCommandHandler
{
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IContributionRepository _contributionRepository;

    public FailContributionPaymentCommandHandler(
        IContributionDateTimeProvider dateTimeProvider,
        IContributionRepository contributionRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _contributionRepository = contributionRepository;
    }

    public async Task<FailContributionPaymentResult> Handle(
        FailContributionPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var contribution = await _contributionRepository.GetByIdAsync(command.ContributionId, cancellationToken);

        if (contribution is null || contribution.CampaignId != command.CampaignId)
        {
            throw new KeyNotFoundException($"Contribution '{command.ContributionId}' was not found for campaign '{command.CampaignId}'.");
        }

        contribution.FailPayment(command.FailureReason, _dateTimeProvider.UtcNow);

        await _contributionRepository.UpdateAsync(contribution, cancellationToken);

        return new FailContributionPaymentResult(
            contribution.Id,
            contribution.Status.ToString(),
            contribution.FailureReason!);
    }
}
