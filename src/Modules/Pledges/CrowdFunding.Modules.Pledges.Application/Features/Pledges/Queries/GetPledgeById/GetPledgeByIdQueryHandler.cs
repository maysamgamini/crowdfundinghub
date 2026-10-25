using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;

/// <summary>
/// Handles Get Pledge By Id query requests. A backer may read their own pledge, the campaign's
/// owner may read any pledge on it without its payment references, and a payments manager may read
/// everything. Anyone else gets "not found", the same answer as for a pledge that does not exist, so
/// the response does not reveal which ids are real.
/// </summary>
public sealed class GetPledgeByIdQueryHandler : IQueryHandler<GetPledgeByIdQuery, GetPledgeByIdResult>
{
    private readonly IPledgeReadService _pledgeReadService;
    private readonly ICurrentUser _currentUser;
    private readonly IActiveCampaignCacheRepository _campaigns;

    public GetPledgeByIdQueryHandler(
        IPledgeReadService pledgeReadService,
        ICurrentUser currentUser,
        IActiveCampaignCacheRepository campaigns)
    {
        _pledgeReadService = pledgeReadService;
        _currentUser = currentUser;
        _campaigns = campaigns;
    }

    public async Task<GetPledgeByIdResult> Handle(GetPledgeByIdQuery query, CancellationToken cancellationToken)
    {
        var viewer = await PledgeVisibility.ResolveAsync(_currentUser, _campaigns, query.CampaignId, cancellationToken);
        var result = await _pledgeReadService.GetByIdAsync(query.CampaignId, query.PledgeId, cancellationToken);

        var notFound = new KeyNotFoundException(
            $"Pledge '{query.PledgeId}' was not found for campaign '{query.CampaignId}'.");

        if (result is null)
        {
            throw notFound;
        }

        if (viewer == PledgeViewer.PaymentsManager || result.BackerId == _currentUser.UserId)
        {
            return result;
        }

        if (viewer == PledgeViewer.CampaignOwner)
        {
            return result with { PaymentReference = null, ExternalPaymentIntentId = null, FailureReason = null };
        }

        throw notFound;
    }
}
