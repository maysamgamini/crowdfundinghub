using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Services;

public sealed class ContributionReadService : IContributionReadService
{
    private readonly ContributionsDbContext _dbContext;

    public ContributionReadService(ContributionsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Contributions
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new ListContributionsByCampaignResult(
                x.Id,
                x.CampaignId,
                x.ContributorId,
                x.Money.Amount,
                x.Money.Currency,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
