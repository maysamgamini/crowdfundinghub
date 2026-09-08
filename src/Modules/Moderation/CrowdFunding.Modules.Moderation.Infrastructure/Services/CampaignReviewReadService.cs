using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using CrowdFunding.Modules.Moderation.Domain.Enums;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

/// <summary>
/// Provides read-model access for Campaign Review.
/// </summary>
public sealed class CampaignReviewReadService : ICampaignReviewReadService
{
    private readonly ModerationDbContext _dbContext;

    public CampaignReviewReadService(ModerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetCampaignReviewByCampaignIdResult?> GetByCampaignIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CampaignReviews
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .Select(x => new GetCampaignReviewByCampaignIdResult(
                x.CampaignId,
                x.Status.ToString(),
                x.ModeratorId,
                x.Notes,
                x.CreatedAtUtc,
                x.ReviewedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<GetCampaignReviewByCampaignIdResult>> ListAsync(
        PageRequest pageRequest,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CampaignReviews.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<CampaignReviewStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        query = query.OrderBy(x => x.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip(pageRequest.Skip)
            .Take(pageRequest.PageSize)
            .Select(x => new GetCampaignReviewByCampaignIdResult(
                x.CampaignId,
                x.Status.ToString(),
                x.ModeratorId,
                x.Notes,
                x.CreatedAtUtc,
                x.ReviewedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<GetCampaignReviewByCampaignIdResult>(
            items,
            pageRequest.PageNumber,
            pageRequest.PageSize,
            totalCount);
    }
}
