using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.LateContributionRejected;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Domain.Enums;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;

/// <summary>
/// Refunds a contribution that the campaign refused to count because the payment was confirmed after
/// the campaign had ended. Safe to run twice: only a contribution that is still <c>Succeeded</c> is refunded.
/// </summary>
public sealed class LateContributionRefundHandler : IEventHandler<LateContributionRejectedApplicationEvent>
{
    private readonly IContributionRepository _contributionRepository;
    private readonly ContributionRefunder _refunder;

    public LateContributionRefundHandler(IContributionRepository contributionRepository, ContributionRefunder refunder)
    {
        _contributionRepository = contributionRepository;
        _refunder = refunder;
    }

    public async Task Handle(LateContributionRejectedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var contribution = await _contributionRepository.GetByIdAsync(notification.ContributionId, cancellationToken);

        if (contribution is null || contribution.Status != ContributionStatus.Succeeded)
        {
            return;
        }

        await _refunder.RefundAsync(contribution, cancellationToken);
    }
}
