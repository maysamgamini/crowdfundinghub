using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetCampaignPayout;

/// <summary>
/// Reads a campaign's payout for its creator, or for a holder of the payments-manage permission. Anyone else, and a
/// campaign without a payout, get "not found": the answer does not reveal which campaigns have a payout.
/// </summary>
public sealed class GetCampaignPayoutQueryHandler : IQueryHandler<GetCampaignPayoutQuery, GetCampaignPayoutResult>
{
    private readonly IPayoutStore _payoutStore;
    private readonly ICurrentUser _currentUser;

    public GetCampaignPayoutQueryHandler(IPayoutStore payoutStore, ICurrentUser currentUser)
    {
        _payoutStore = payoutStore;
        _currentUser = currentUser;
    }

    public async Task<GetCampaignPayoutResult> Handle(GetCampaignPayoutQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to read a payout.");
        }

        var notFound = new KeyNotFoundException($"Campaign '{query.CampaignId}' has no payout.");
        var payout = await _payoutStore.GetByCampaignAsync(query.CampaignId, cancellationToken) ?? throw notFound;

        if (payout.OwnerId != _currentUser.UserId && !_currentUser.HasPermission(PermissionConstants.PledgesPaymentsManage))
        {
            throw notFound;
        }

        var ledger = await _payoutStore.GetLedgerAsync(payout.Id, cancellationToken);

        return new GetCampaignPayoutResult(
            payout.Id,
            payout.CampaignId,
            payout.Status.ToString(),
            payout.Currency,
            payout.GrossAmount,
            payout.FeeAmount,
            payout.NetAmount,
            payout.PaidAtUtc,
            ledger.Sum(x => x.Amount),
            ledger.Select(x => new PayoutLedgerLineResult(x.Kind.ToString(), x.Amount, x.PledgeId, x.CreatedAtUtc)).ToList());
    }
}
