using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.GetContributionById;

/// <summary>
/// Handles Get Contribution By Id query requests.
/// </summary>
public sealed class GetContributionByIdQueryHandler : IQueryHandler<GetContributionByIdQuery, GetContributionByIdResult>
{
    private readonly IContributionReadService _contributionReadService;

    public GetContributionByIdQueryHandler(IContributionReadService contributionReadService)
    {
        _contributionReadService = contributionReadService;
    }

    public async Task<GetContributionByIdResult> Handle(GetContributionByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await _contributionReadService.GetByIdAsync(query.CampaignId, query.ContributionId, cancellationToken);

        return result ?? throw new NotFoundException(
            $"Contribution '{query.ContributionId}' was not found for campaign '{query.CampaignId}'.");
    }
}
