using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

/// <summary>
/// Handles List Contributions By Campaign query requests. A payments manager sees every pledge, the
/// campaign's owner sees every pledge without payment references, and anyone else signed in sees only
/// their own pledges on the campaign.
/// </summary>
public sealed class ListContributionsByCampaignQueryHandler : IQueryHandler<ListContributionsByCampaignQuery, PagedResult<ListContributionsByCampaignResult>>
{
    private readonly IContributionReadService _contributionReadService;
    private readonly ICurrentUser _currentUser;
    private readonly IActiveCampaignCacheRepository _campaigns;

    public ListContributionsByCampaignQueryHandler(
        IContributionReadService contributionReadService,
        ICurrentUser currentUser,
        IActiveCampaignCacheRepository campaigns)
    {
        _contributionReadService = contributionReadService;
        _currentUser = currentUser;
        _campaigns = campaigns;
    }

    public async Task<PagedResult<ListContributionsByCampaignResult>> Handle(
        ListContributionsByCampaignQuery query,
        CancellationToken cancellationToken)
    {
        var viewer = await ContributionVisibility.ResolveAsync(_currentUser, _campaigns, query.CampaignId, cancellationToken);

        if (viewer == ContributionViewer.Backer)
        {
            // Asking for someone else's pledges is answered with an empty page, not an error.
            if (query.Filter.ContributorId is { } requested && requested != _currentUser.UserId)
            {
                return new PagedResult<ListContributionsByCampaignResult>(
                    [], query.PageRequest.PageNumber, query.PageRequest.PageSize, 0);
            }

            var own = query.Filter with { ContributorId = _currentUser.UserId };
            return await _contributionReadService.ListByCampaignAsync(query.CampaignId, query.PageRequest, own, cancellationToken);
        }

        var page = await _contributionReadService.ListByCampaignAsync(
            query.CampaignId, query.PageRequest, query.Filter, cancellationToken);

        if (viewer == ContributionViewer.PaymentsManager)
        {
            return page;
        }

        var redacted = page.Items
            .Select(item => item with { PaymentReference = null, FailureReason = null })
            .ToArray();
        return new PagedResult<ListContributionsByCampaignResult>(redacted, page.PageNumber, page.PageSize, page.TotalCount);
    }
}
