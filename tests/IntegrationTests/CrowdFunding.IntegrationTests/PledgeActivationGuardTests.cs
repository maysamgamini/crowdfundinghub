using System.Net;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Migrations;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// A pledge is activated at most once. The aggregate checks the status first; these tests show the database holding
/// the line when that check is bypassed (plain SQL) or beaten (two requests at the same moment).
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class PledgeActivationGuardTests
{
    private readonly CrowdFundingApiFactory _factory;

    public PledgeActivationGuardTests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid CampaignId, Guid PledgeId, string AdminToken)> PendingPledgeAsync(string label, HttpClient client)
    {
        var createdAtUtc = DateTime.UtcNow.AddMinutes(-10);
        var campaign = Campaign.Create(
            Guid.NewGuid(), "Activation Guard Campaign",
            "A story that is definitely longer than twenty characters to satisfy validation rules.",
            "Technology", new Money(10_000m, "USD"), DateTime.UtcNow.AddDays(30), createdAtUtc);
        campaign.Publish(createdAtUtc.AddMinutes(1));
        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
            var executor = scope.ServiceProvider.GetRequiredService<ICampaignTransactionExecutor>();
            await executor.ExecuteAsync(ct => repository.AddAsync(campaign, ct), CancellationToken.None);
        }

        await _factory.ProcessOutboxMessagesAsync();

        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail($"{label}-backer"), "Backer");
        client.SetBearerToken(backerToken);
        var made = (await (await client.PostAsJsonAsync($"/api/campaigns/{campaign.Id}/contributions", new MakeContributionRequest(100m, "USD")))
            .Content.ReadFromJsonAsync<MakeContributionResponse>())!;

        var adminEmail = ApiTestExtensions.UniqueEmail($"{label}-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        return (campaign.Id, made.ContributionId, await client.LoginUserAsync(adminEmail));
    }

    [Fact]
    public async Task ASecondActivationRowForTheSamePledge_IsRefusedByTheDatabase()
    {
        using var client = _factory.CreateClient();
        var (_, pledgeId, _) = await PendingPledgeAsync("activation-sql", client);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        db.PledgeActivations.Add(new PledgeActivation(pledgeId, "pm_ok_first", DateTime.UtcNow));
        await db.SaveChangesAsync();

        db.PledgeActivations.Add(new PledgeActivation(pledgeId, "pm_ok_second", DateTime.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(ContributionIndexes.OneActivationPerPledge, postgres.ConstraintName);
    }

    [Fact]
    public async Task TheSameSetupNewsSentManyTimesAtOnce_ActivatesOnce_AndNeverFailsTheLosers()
    {
        using var setup = _factory.CreateClient();
        var (campaignId, pledgeId, adminToken) = await PendingPledgeAsync("activation-race", setup);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var client = _factory.CreateClient();
            client.SetBearerToken(adminToken);
            return await client.PostAsJsonAsync(
                $"/api/campaigns/{campaignId}/contributions/{pledgeId}/confirm-setup",
                new ConfirmPledgeSetupRequest("pm_ok_race"));
        }));

        // A loser that lost at the row version may be told to retry (409); none may fail any other way.
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, r.StatusCode.ToString()));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        Assert.Equal(1, await db.PledgeActivations.CountAsync(a => a.ContributionId == pledgeId));
        Assert.Equal(ContributionStatus.Active, (await db.Contributions.AsNoTracking().SingleAsync(c => c.Id == pledgeId)).Status);
    }
}
