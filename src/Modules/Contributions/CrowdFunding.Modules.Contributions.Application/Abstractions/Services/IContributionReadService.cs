using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;

namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

public interface IContributionReadService
{
    Task<IReadOnlyCollection<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken);
}
