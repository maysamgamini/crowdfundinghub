using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;

/// <summary>
/// Handles List Pledges By Campaign query requests. A payments manager sees every pledge, the
/// campaign's owner sees every pledge without payment references, and anyone else signed in sees only
/// their own pledges on the campaign.
/// </summary>
public sealed class ListPledgesByCampaignQueryHandler : IQueryHandler<ListPledgesByCampaignQuery, PagedResult<ListPledgesByCampaignResult>>
{
    private readonly IPledgeReadService _pledgeReadService;
    private readonly ICurrentUser _currentUser;
    private readonly IActiveCampaignCacheRepository _campaigns;

    public ListPledgesByCampaignQueryHandler(
        IPledgeReadService pledgeReadService,
        ICurrentUser currentUser,
        IActiveCampaignCacheRepository campaigns)
    {
        _pledgeReadService = pledgeReadService;
        _currentUser = currentUser;
        _campaigns = campaigns;
    }

    public async Task<PagedResult<ListPledgesByCampaignResult>> Handle(
        ListPledgesByCampaignQuery query,
        CancellationToken cancellationToken)
    {
        var viewer = await PledgeVisibility.ResolveAsync(_currentUser, _campaigns, query.CampaignId, cancellationToken);

        if (viewer == PledgeViewer.Backer)
        {
            // Asking for someone else's pledges is answered with an empty page, not an error.
            if (query.Filter.BackerId is { } requested && requested != _currentUser.UserId)
            {
                return new PagedResult<ListPledgesByCampaignResult>(
                    [], query.PageRequest.PageNumber, query.PageRequest.PageSize, 0);
            }

            var own = query.Filter with { BackerId = _currentUser.UserId };
            return await _pledgeReadService.ListByCampaignAsync(query.CampaignId, query.PageRequest, own, cancellationToken);
        }

        var page = await _pledgeReadService.ListByCampaignAsync(
            query.CampaignId, query.PageRequest, query.Filter, cancellationToken);

        if (viewer == PledgeViewer.PaymentsManager)
        {
            return page;
        }

        var redacted = page.Items
            .Select(item => item with { PaymentReference = null, FailureReason = null })
            .ToArray();
        return new PagedResult<ListPledgesByCampaignResult>(redacted, page.PageNumber, page.PageSize, page.TotalCount);
    }
}
