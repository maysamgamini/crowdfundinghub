using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.LatePledgeRejected;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Domain.Enums;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;

/// <summary>
/// Refunds a pledge that the campaign refused to count because the payment was confirmed after
/// the campaign had ended. Safe to run twice: only a pledge that is still <c>Succeeded</c> is refunded.
/// </summary>
public sealed class LatePledgeRefundHandler : IEventHandler<LatePledgeRejectedApplicationEvent>
{
    private readonly IPledgeRepository _pledgeRepository;
    private readonly PledgeRefunder _refunder;

    public LatePledgeRefundHandler(IPledgeRepository pledgeRepository, PledgeRefunder refunder)
    {
        _pledgeRepository = pledgeRepository;
        _refunder = refunder;
    }

    public async Task Handle(LatePledgeRejectedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var pledge = await _pledgeRepository.GetByIdAsync(notification.PledgeId, cancellationToken);

        if (pledge is null || pledge.Status != PledgeStatus.Succeeded)
        {
            return;
        }

        await _refunder.RefundAsync(pledge, cancellationToken);
    }
}
