using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Infrastructure.Payments;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The refund job against a real database and the fake provider: a pass starts only as many refunds as the rate limit
/// allows, the next pass resumes, every answered attempt is a row, and the database holds the one-job-per-campaign rule.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RefundJobDatabaseTests
{
    private readonly CrowdFundingApiFactory _factory;

    public RefundJobDatabaseTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid CampaignId, Guid[] PledgeIds)> SeedJobAsync(int pledges)
    {
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, pledges).Select(i =>
        {
            var pledge = Pledge.Create(campaignId, Guid.NewGuid(), 10m, "USD", now.AddSeconds(i));
            pledge.AttachPaymentIntent($"pi_job_{Guid.NewGuid():N}", "Mock");
            pledge.ConfirmPayment($"ref-{Guid.NewGuid():N}", now);
            return pledge;
        }).ToArray();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.Pledges.AddRange(rows);
        db.RefundJobs.Add(RefundJob.Create(campaignId, now));
        await db.SaveChangesAsync();
        return (campaignId, rows.Select(x => x.Id).ToArray());
    }

    private async Task<int> PassAsync(int maxRefunds)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RefundJobService>().RefundDueAsync(maxRefunds, CancellationToken.None);
    }

    private async Task<(int Refunded, RefundJobStatus Job, int Attempts)> StateAsync(Guid campaignId, Guid[] pledgeIds)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        return (
            await db.Pledges.CountAsync(x => pledgeIds.Contains(x.Id) && x.Status == PledgeStatus.Refunded),
            (await db.RefundJobs.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId)).Status,
            await db.RefundAttempts.CountAsync(x => pledgeIds.Contains(x.PledgeId)));
    }

    [Fact]
    public async Task APass_RefundsOnlyAsManyAsTheRateLimitAllows_AndTheNextPassResumes()
    {
        var (campaignId, pledgeIds) = await SeedJobAsync(5);

        // The shared database may hold other open jobs, so count only this campaign's pledges.
        while ((await StateAsync(campaignId, pledgeIds)).Refunded == 0)
        {
            await PassAsync(1);
        }

        var afterFirst = await StateAsync(campaignId, pledgeIds);
        Assert.True(afterFirst.Refunded < 5);
        Assert.Equal(RefundJobStatus.Open, afterFirst.Job);

        while ((await StateAsync(campaignId, pledgeIds)).Job == RefundJobStatus.Open)
        {
            await PassAsync(2);
        }

        var done = await StateAsync(campaignId, pledgeIds);
        Assert.Equal(5, done.Refunded);
        Assert.Equal(RefundJobStatus.Completed, done.Job);
        Assert.Equal(5, done.Attempts);
    }

    [Fact]
    public async Task ASecondRefundJobForTheSameCampaign_IsRefusedByTheDatabase()
    {
        var (campaignId, pledgeIds) = await SeedJobAsync(1);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();

        db.RefundJobs.Add(RefundJob.Create(campaignId, DateTime.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(PledgeIndexes.OneRefundJobPerCampaign, postgres.ConstraintName);
        while ((await StateAsync(campaignId, pledgeIds)).Job == RefundJobStatus.Open)
        {
            await PassAsync(10);
        }
    }

    [Fact]
    public async Task ASecondRecordOfTheSameAttempt_IsRefusedByTheDatabase()
    {
        var (campaignId, pledgeIds) = await SeedJobAsync(1);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.RefundAttempts.Add(RefundAttempt.Failed(pledgeIds[0], 1, "key-1", "code", DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.RefundAttempts.Add(RefundAttempt.Failed(pledgeIds[0], 1, "key-1-again", "code", DateTime.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PledgeIndexes.OneRefundAttemptNumberPerPledge, postgres.ConstraintName);

        // The made-up attempt row would block this pledge's real attempt 1 for ever, so close the job by hand
        // instead of draining it: the shared database must not keep a job no pass can finish.
        await using var cleanup = _factory.Services.CreateAsyncScope();
        await cleanup.ServiceProvider.GetRequiredService<PledgesDbContext>().Database.ExecuteSqlRawAsync(
            "UPDATE pledges.refund_jobs SET \"Status\" = 'NeedsAttention', \"FinishedAtUtc\" = now() WHERE \"CampaignId\" = {0}",
            campaignId);
    }

    [Fact]
    public async Task AFailedAttemptWithoutAFailureCode_IsRefusedByTheDatabase()
    {
        var (campaignId, pledgeIds) = await SeedJobAsync(1);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO pledges.refund_attempts (\"Id\", \"PledgeId\", \"AttemptNumber\", \"IdempotencyKey\", \"Outcome\", \"CreatedAtUtc\") " +
            "VALUES (gen_random_uuid(), {0}, 1, 'k', 'Failed', now())",
            pledgeIds[0]));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        while ((await StateAsync(campaignId, pledgeIds)).Job == RefundJobStatus.Open)
        {
            await PassAsync(10);
        }
    }
}
