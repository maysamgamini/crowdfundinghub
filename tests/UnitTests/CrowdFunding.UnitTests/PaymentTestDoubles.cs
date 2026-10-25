using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;

namespace CrowdFunding.UnitTests;

/// <summary>Settings for the unit tests of the funding model; the defaults match the real defaults.</summary>
internal sealed class FakePaymentSettings(FundingModel fundingModel = FundingModel.ChargeNow) : IPaymentSettings
{
    public FundingModel FundingModel { get; } = fundingModel;

    public int MaxChargeAttempts { get; init; } = 3;

    public TimeSpan ChargeRetryDelay { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan ChargeLease { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan PayoutRetryDelay { get; init; } = TimeSpan.FromHours(1);

    public int RefundsPerPass { get; init; } = 50;

    public TimeSpan RefundRetryDelay { get; init; } = TimeSpan.FromMinutes(15);

    public int MaxRefundAttempts { get; init; } = 5;
}

/// <summary>In-memory refund store over a list of pledges, for the unit tests of the refund job.</summary>
internal sealed class FakeRefundStore(IEnumerable<Pledge> pledges) : IRefundStore
{
    private readonly List<Pledge> _pledges = pledges as List<Pledge> ?? pledges.ToList();

    public List<RefundJob> Jobs { get; } = [];

    public List<RefundAttempt> Attempts { get; } = [];

    public Task<bool> JobExistsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
        => Task.FromResult(Jobs.Any(x => x.CampaignId == campaignId));

    public Task AddJobAsync(RefundJob job, CancellationToken cancellationToken)
    {
        Jobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RefundJob>> GetOpenJobsAsync(int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RefundJob>>(Jobs.Where(x => x.Status == RefundJobStatus.Open).OrderBy(x => x.CreatedAtUtc).Take(limit).ToList());

    public Task UpdateJobAsync(RefundJob job, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<Pledge>> GetRefundablePledgesAsync(Guid campaignId, DateTime nowUtc, int maxAttempts, int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Pledge>>(_pledges
            .Where(x => x.CampaignId == campaignId
                        && x.Status == PledgeStatus.Succeeded
                        && x.RefundFailures < maxAttempts
                        && (x.NextRefundAtUtc == null || x.NextRefundAtUtc <= nowUtc))
            .Take(limit)
            .ToList());

    public Task<RefundProgress> GetProgressAsync(Guid campaignId, int maxAttempts, CancellationToken cancellationToken)
    {
        var open = _pledges.Where(x => x.CampaignId == campaignId && x.Status == PledgeStatus.Succeeded).ToList();
        return Task.FromResult(new RefundProgress(open.Count, open.Count(x => x.RefundFailures >= maxAttempts)));
    }

    public Task AddAttemptAsync(RefundAttempt attempt, CancellationToken cancellationToken)
    {
        Attempts.Add(attempt);
        return Task.CompletedTask;
    }
}
