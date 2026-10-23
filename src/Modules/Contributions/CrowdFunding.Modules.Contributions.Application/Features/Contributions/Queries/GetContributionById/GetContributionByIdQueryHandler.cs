using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;

/// <summary>
/// Handles Get Contribution By Id query requests. A backer may read their own pledge, the campaign's
/// owner may read any pledge on it without its payment references, and a payments manager may read
/// everything. Anyone else gets "not found", the same answer as for a pledge that does not exist, so
/// the response does not reveal which ids are real.
/// </summary>
public sealed class GetContributionByIdQueryHandler : IQueryHandler<GetContributionByIdQuery, GetContributionByIdResult>
{
    private readonly IContributionReadService _contributionReadService;
    private readonly ICurrentUser _currentUser;
    private readonly IActiveCampaignCacheRepository _campaigns;

    public GetContributionByIdQueryHandler(
        IContributionReadService contributionReadService,
        ICurrentUser currentUser,
        IActiveCampaignCacheRepository campaigns)
    {
        _contributionReadService = contributionReadService;
        _currentUser = currentUser;
        _campaigns = campaigns;
    }

    public async Task<GetContributionByIdResult> Handle(GetContributionByIdQuery query, CancellationToken cancellationToken)
    {
        var viewer = await ContributionVisibility.ResolveAsync(_currentUser, _campaigns, query.CampaignId, cancellationToken);
        var result = await _contributionReadService.GetByIdAsync(query.CampaignId, query.ContributionId, cancellationToken);

        var notFound = new KeyNotFoundException(
            $"Contribution '{query.ContributionId}' was not found for campaign '{query.CampaignId}'.");

        if (result is null)
        {
            throw notFound;
        }

        if (viewer == ContributionViewer.PaymentsManager || result.ContributorId == _currentUser.UserId)
        {
            return result;
        }

        if (viewer == ContributionViewer.CampaignOwner)
        {
            return result with { PaymentReference = null, ExternalPaymentIntentId = null, FailureReason = null };
        }

        throw notFound;
    }
}
