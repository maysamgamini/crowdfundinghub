using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// A campaign is paid out at most once and the amounts of a payout add up. The domain checks both; these tests show
/// the database holding the line when the domain is bypassed.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PayoutDatabaseGuardTests
{
    private readonly CrowdFundingApiFactory _factory;

    public PayoutDatabaseGuardTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ASecondPayoutForTheSameCampaign_IsRefusedByTheDatabase()
    {
        var campaignId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.Payouts.Add(Payout.Create(campaignId, Guid.NewGuid(), "USD", DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.Payouts.Add(Payout.Create(campaignId, Guid.NewGuid(), "USD", DateTime.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(PledgeIndexes.OnePayoutPerCampaign, postgres.ConstraintName);
    }

    [Fact]
    public async Task AmountsThatDoNotAddUp_AreRefusedByTheDatabase()
    {
        var campaignId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PledgesDbContext>();
        db.Payouts.Add(Payout.Create(campaignId, Guid.NewGuid(), "USD", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE pledges.payouts SET \"GrossAmount\" = 100, \"FeeAmount\" = 5, \"NetAmount\" = 100 WHERE \"CampaignId\" = {0}",
            campaignId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(PledgeIndexes.PayoutAmountsAddUp, exception.ConstraintName);
    }
}
