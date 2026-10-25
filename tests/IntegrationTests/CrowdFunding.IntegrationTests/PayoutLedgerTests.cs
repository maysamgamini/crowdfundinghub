using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges;
using CrowdFunding.Modules.Pledges.Contracts.Events.PayoutPaid;
using Microsoft.AspNetCore.Mvc.Testing;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

#pragma warning disable EF1002 // the statements use literals and ids the test made, never user input

/// <summary>
/// The payout ledger against a real database: each charged pledge and its fee are lines, paying the creator closes the
/// balance to zero, a pledge refunded after the amounts were fixed is recorded as money the creator owes back, and the
/// database refuses to change or delete a line.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PayoutLedgerTests
{
    private readonly CrowdFundingApiFactory _factory;

    public PayoutLedgerTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid CampaignId, Guid[] PledgeIds)> SeedSucceededCampaignAsync(params decimal[] amounts)
    {
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var pledges = amounts.Select(amount =>
        {
            var pledge = Pledge.Create(campaignId, Guid.NewGuid(), amount, "USD", now);
            pledge.ConfirmPayment($"ref-{Guid.NewGuid():N}", now);
            return pledge;
        }).ToArray();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.Pledges.AddRange(pledges);
        db.Payouts.Add(Payout.Create(campaignId, Guid.NewGuid(), "USD", now));
        await db.SaveChangesAsync();
        return (campaignId, pledges.Select(x => x.Id).ToArray());
    }

    // The database is shared with other tests whose workers pay every open payout they find, so a test that leaves
    // a payout open closes it as an empty one.
    private async Task CloseQuietlyAsync(Guid campaignId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE pledges.payouts SET \"Status\" = 'NothingToPay', \"GrossAmount\" = 0, \"FeeAmount\" = 0, \"NetAmount\" = 0 " +
            "WHERE \"CampaignId\" = {0} AND \"Status\" IN ('Pending', 'Ready')",
            campaignId);
    }

    private async Task RunAsync(Func<PayoutService, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PayoutService>());
    }

    private async Task<(Payout Payout, List<PayoutLedgerEntry> Ledger)> LedgerOfAsync(Guid campaignId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var payout = await db.Payouts.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId);
        var ledger = await db.PayoutLedgerEntries.AsNoTracking().Where(x => x.PayoutId == payout.Id).ToListAsync();
        return (payout, ledger);
    }

    [Fact]
    public async Task PayingTheCreator_WritesACharge_AndAFee_PerPledge_AndClosesTheBalanceToZero()
    {
        var (campaignId, _) = await SeedSucceededCampaignAsync(33.33m, 33.33m, 33.33m);

        await RunAsync(service => service.PayDueAsync(50, CancellationToken.None));

        var (payout, ledger) = await LedgerOfAsync(campaignId);
        Assert.Equal(PayoutStatus.Paid, payout.Status);
        Assert.Equal(99.99m, payout.GrossAmount);
        Assert.Equal(5.01m, payout.FeeAmount);
        Assert.Equal(3, ledger.Count(x => x.Kind == PayoutLedgerKind.Charge));
        Assert.Equal(3, ledger.Count(x => x.Kind == PayoutLedgerKind.PlatformFee));
        Assert.Equal(1, ledger.Count(x => x.Kind == PayoutLedgerKind.PaidOut));
        Assert.Equal(0m, ledger.Sum(x => x.Amount));
    }

    [Fact]
    public async Task APledgeRefundedAfterThePayout_IsRecordedAsWhatTheCreatorOwesBack_OnceHoweverOftenTheWorkerRuns()
    {
        var (campaignId, pledgeIds) = await SeedSucceededCampaignAsync(33.33m, 66.67m);
        await RunAsync(service => service.PayDueAsync(50, CancellationToken.None));

        // The creator was paid for both pledges; then one of them is refunded.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
            var pledge = await db.Pledges.SingleAsync(x => x.Id == pledgeIds[0]);
            pledge.Refund(DateTime.UtcNow, "refund-ref");
            await db.SaveChangesAsync();
        }

        await RunAsync(service => service.RecordRefundsAfterPayoutAsync(50, CancellationToken.None));
        await RunAsync(service => service.RecordRefundsAfterPayoutAsync(50, CancellationToken.None));

        var (_, ledger) = await LedgerOfAsync(campaignId);
        var refunded = Assert.Single(ledger, x => x.Kind == PayoutLedgerKind.ChargeRefunded);
        var returned = Assert.Single(ledger, x => x.Kind == PayoutLedgerKind.FeeReturned);
        Assert.Equal(-33.33m, refunded.Amount);
        Assert.Equal(1.67m, returned.Amount);
        Assert.Equal(pledgeIds[0], refunded.PledgeId);

        // The balance is what the creator owes: the refunded amount less the fee the platform gives back.
        Assert.Equal(-31.66m, ledger.Sum(x => x.Amount));
    }

    [Fact]
    public async Task APledgeRefundedBeforeTheAmountsWereFixed_IsNeverCounted_SoNothingIsOwedBack()
    {
        var (campaignId, pledgeIds) = await SeedSucceededCampaignAsync(40m, 60m);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
            var pledge = await db.Pledges.SingleAsync(x => x.Id == pledgeIds[0]);
            pledge.Refund(DateTime.UtcNow, "refund-ref");
            await db.SaveChangesAsync();
        }

        await RunAsync(service => service.PayDueAsync(50, CancellationToken.None));
        await RunAsync(service => service.RecordRefundsAfterPayoutAsync(50, CancellationToken.None));

        var (payout, ledger) = await LedgerOfAsync(campaignId);
        Assert.Equal(60m, payout.GrossAmount);
        Assert.DoesNotContain(ledger, x => x.Kind == PayoutLedgerKind.ChargeRefunded);
        Assert.Equal(0m, ledger.Sum(x => x.Amount));
    }

    [Fact]
    public async Task PayingTheCreator_PublishesAPayoutPaidEvent_ThroughTheOutbox()
    {
        var probe = new PayoutPaidProbe();
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(probe);
            services.AddScoped<IEventHandler<PayoutPaidApplicationEvent>, PayoutPaidProbeHandler>();
        }));
        _ = host.Server;   // starts the host and its outbox worker
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var pledge = Pledge.Create(campaignId, Guid.NewGuid(), 10m, "USD", now);
        pledge.ConfirmPayment("probe-ref", now);
        Guid payoutId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
            db.Pledges.Add(pledge);
            var payout = Payout.Create(campaignId, Guid.NewGuid(), "USD", now);
            payoutId = payout.Id;
            db.Payouts.Add(payout);
            await db.SaveChangesAsync();
        }

        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PayoutService>().PayDueAsync(50, CancellationToken.None);
        }

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && !probe.Received.Any(x => x.PayoutId == payoutId))
        {
            await Task.Delay(100);
        }

        var received = Assert.Single(probe.Received, x => x.PayoutId == payoutId);
        Assert.Equal(campaignId, received.CampaignId);
        Assert.Equal(10m, received.GrossAmount);
        Assert.Equal(0.50m, received.FeeAmount);
        Assert.Equal(9.50m, received.NetAmount);
    }

    private sealed class PayoutPaidProbe
    {
        public System.Collections.Concurrent.ConcurrentBag<PayoutPaidApplicationEvent> Received { get; } = [];
    }

    private sealed class PayoutPaidProbeHandler(PayoutPaidProbe probe) : IEventHandler<PayoutPaidApplicationEvent>
    {
        public Task Handle(PayoutPaidApplicationEvent notification, CancellationToken cancellationToken)
        {
            probe.Received.Add(notification);
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("UPDATE pledges.payout_ledger_entries SET \"Amount\" = \"Amount\" WHERE \"PayoutId\" = {0}")]
    [InlineData("DELETE FROM pledges.payout_ledger_entries WHERE \"PayoutId\" = {0}")]
    public async Task ALedgerLine_CannotBeChangedOrDeleted(string statement)
    {
        var (campaignId, _) = await SeedSucceededCampaignAsync(20m);
        await RunAsync(service => service.PayDueAsync(50, CancellationToken.None));
        var (payout, _) = await LedgerOfAsync(campaignId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(statement, payout.Id));

        Assert.Equal("55000", exception.SqlState);
    }

    [Fact]
    public async Task ALedgerLineWithTheWrongSignForItsKind_IsRefusedByTheDatabase()
    {
        var (campaignId, pledgeIds) = await SeedSucceededCampaignAsync(20m);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var payoutId = (await db.Payouts.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId)).Id;

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO pledges.payout_ledger_entries (\"Id\", \"PayoutId\", \"Kind\", \"Amount\", \"Currency\", \"PledgeId\", \"CreatedAtUtc\") " +
            "VALUES (gen_random_uuid(), {0}, 'Charge', -20, 'USD', {1}, now())",
            payoutId,
            pledgeIds[0]));
        await CloseQuietlyAsync(campaignId);

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(PledgeIndexes.PayoutLedgerSignMatchesKind, exception.ConstraintName);
    }

    [Fact]
    public async Task ASecondLineOfTheSameKindForTheSamePledge_IsRefusedByTheDatabase()
    {
        var (campaignId, pledgeIds) = await SeedSucceededCampaignAsync(20m);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        var payoutId = (await db.Payouts.AsNoTracking().SingleAsync(x => x.CampaignId == campaignId)).Id;
        db.PayoutLedgerEntries.Add(PayoutLedgerEntry.Charge(payoutId, pledgeIds[0], 20m, "USD", DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.PayoutLedgerEntries.Add(PayoutLedgerEntry.Charge(payoutId, pledgeIds[0], 20m, "USD", DateTime.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PledgeIndexes.OnePayoutLedgerLinePerPledge, postgres.ConstraintName);
        await CloseQuietlyAsync(campaignId);
    }
}
