using CrowdFunding.BuildingBlocks.Application.Pagination;
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

    public async Task<PagedResult<ListContributionsByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Contributions
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(pageRequest.Skip)
            .Take(pageRequest.PageSize)
            .Select(x => new ListContributionsByCampaignResult(
                x.Id,
                x.CampaignId,
                x.ContributorId,
                x.Money.Amount,
                x.Money.Currency,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListContributionsByCampaignResult>(
            items,
            pageRequest.PageNumber,
            pageRequest.PageSize,
            totalCount);
    }
}
