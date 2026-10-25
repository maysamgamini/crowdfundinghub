using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Services;

/// <summary>
/// Provides read-model access for Pledge.
/// </summary>
public sealed class PledgeReadService : IPledgeReadService
{
    private readonly PledgesDbContext _dbContext;

    public PledgeReadService(PledgesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ListPledgesByCampaignResult>> ListByCampaignAsync(
        Guid campaignId,
        PageRequest pageRequest,
        ListPledgesByCampaignFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Pledges
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId);

        if (filter.BackerId.HasValue)
        {
            query = query.Where(x => x.BackerId == filter.BackerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Currency))
        {
            var currency = filter.Currency.Trim().ToUpper();
            query = query.Where(x => x.Money.Currency == currency);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (Enum.TryParse<PledgeStatus>(filter.Status, true, out var status))
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
            .Select(x => new ListPledgesByCampaignResult(
                x.Id,
                x.CampaignId,
                x.BackerId,
                x.Money.Amount,
                x.Money.Currency,
                x.Status.ToString(),
                x.PaymentReference,
                x.FailureReason,
                x.CreatedAtUtc,
                x.ProcessedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ListPledgesByCampaignResult>(
            items,
            pageRequest.PageNumber,
            pageRequest.PageSize,
            totalCount);
    }

    public async Task<GetPledgeByIdResult?> GetByIdAsync(
        Guid campaignId,
        Guid pledgeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Pledges
            .AsNoTracking()
            .Where(x => x.Id == pledgeId && x.CampaignId == campaignId)
            .Select(x => new GetPledgeByIdResult(
                x.Id,
                x.CampaignId,
                x.BackerId,
                x.Money.Amount,
                x.Money.Currency,
                x.Status.ToString(),
                x.PaymentReference,
                x.FailureReason,
                x.CreatedAtUtc,
                x.ProcessedAtUtc,
                x.ExternalPaymentIntentId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
