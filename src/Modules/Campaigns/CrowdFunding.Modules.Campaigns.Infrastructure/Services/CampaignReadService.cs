using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

public sealed class CampaignReadService : ICampaignReadService
{
    private readonly CampaignsDbContext _dbContext;

    public CampaignReadService(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetCampaignByIdResult?> GetByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Campaigns
            .AsNoTracking()
            .Where(x => x.Id == campaignId)
            .Select(x => new GetCampaignByIdResult(
                x.Id,
                x.OwnerId,
                x.Title,
                x.Story,
                x.Category,
                x.GoalAmount.Amount,
                x.GoalAmount.Currency,
                x.RaisedAmount.Amount,
                x.RaisedAmount.Currency,
                x.DeadlineUtc,
                x.Status.ToString(),
                x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<ListCampaignsResult>> ListAsync(
        PageRequest pageRequest,
        ListCampaignsFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Campaigns
            .AsNoTracking()
            .AsQueryable();

        if (filter.OwnerId.HasValue)
        {
            query = query.Where(x => x.OwnerId == filter.OwnerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim().ToLower();
            query = query.Where(x => x.Category.ToLower() == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (Enum.TryParse<CampaignStatus>(filter.Status, true, out var status))
            {
                query = query.Where(x => x.Status == status);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        query = query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(pageRequest.Skip)
            .Take(pageRequest.PageSize)
            .Select(x => new ListCampaignsResult(
                x.Id,
                x.OwnerId,
                x.Title,
                x.Category,
                x.GoalAmount.Amount,
                x.GoalAmount.Currency,
                x.RaisedAmount.Amount,
                x.RaisedAmount.Currency,
                x.DeadlineUtc,
                x.Status.ToString(),
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListCampaignsResult>(
            items,
            pageRequest.PageNumber,
            pageRequest.PageSize,
            totalCount);
    }
}
