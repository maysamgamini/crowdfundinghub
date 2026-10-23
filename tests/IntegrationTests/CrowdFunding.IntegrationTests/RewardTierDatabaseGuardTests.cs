using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The oversell guard lives in the aggregate and under an advisory lock. The database repeats it, so a write
/// path that forgets the lock still cannot sell more slots than the tier holds. These tests go around the
/// aggregate with plain SQL, the way such a path would, and watch the database refuse.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class RewardTierDatabaseGuardTests
{
    private readonly CrowdFundingApiFactory _factory;

    public RewardTierDatabaseGuardTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedTierAsync(int capacity)
    {
        var tier = RewardTier.Create(Guid.NewGuid(), "Early bird", "A limited reward", new Money(10m, "USD"), capacity, DateTime.UtcNow);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        db.Add(tier);
        await db.SaveChangesAsync();
        return tier.Id;
    }

    private async Task<int> SetCountsAsync(Guid tierId, int claimed, int reserved)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        return await db.Database.ExecuteSqlRawAsync(
            "UPDATE campaigns.reward_tiers SET \"ClaimedCount\" = {1}, \"ReservedCount\" = {2} WHERE \"Id\" = {0}",
            tierId, claimed, reserved);
    }

    [Fact]
    public async Task ClaimedPlusReservedAboveTheCapacity_IsRefusedByTheDatabase()
    {
        var tierId = await SeedTierAsync(capacity: 5);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () => await SetCountsAsync(tierId, claimed: 4, reserved: 2));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("CK_reward_tiers_never_oversold", exception.ConstraintName);
    }

    [Fact]
    public async Task ANegativeCount_IsRefusedByTheDatabase()
    {
        var tierId = await SeedTierAsync(capacity: 5);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () => await SetCountsAsync(tierId, claimed: -1, reserved: 0));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task EverySlotTaken_ExactlyAtTheCapacity_IsAllowed()
    {
        var tierId = await SeedTierAsync(capacity: 5);

        Assert.Equal(1, await SetCountsAsync(tierId, claimed: 3, reserved: 2));
    }
}
