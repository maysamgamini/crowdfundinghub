using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-038: the active outbox table must hold only pending/in-flight rows — a successfully
/// published or terminally dead-lettered message is deleted immediately rather than left behind
/// forever under a mutated <c>Status</c>, which is exactly the PostgreSQL MVCC write-amplification
/// trap (unbounded dead-tuple accumulation) this ticket exists to prevent.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OutboxLeanTableE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public OutboxLeanTableE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateCampaignRequest ValidCampaignRequest(string title) => new(
        title,
        "A story that is definitely longer than twenty characters, describing the campaign's purpose.",
        "Technology",
        5000m,
        "USD",
        DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task SuccessfullyPublishedMessage_IsDeletedImmediately_NotLeftBehindAsProcessed()
    {
        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("outbox-lean-success"));
        client.SetBearerToken(token);

        var createResponse = await client.PostAsJsonAsync("/api/campaigns", ValidCampaignRequest("Outbox Lean Table Campaign"));
        createResponse.EnsureSuccessStatusCode();

        await _factory.ProcessOutboxMessagesAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();

        // A message that finished publishing has no reason to still exist in the active table
        // under any status — delete-on-success means "Processed" is a transient in-memory state
        // during the batch, never a row that lands back in the database.
        Assert.Empty(await dbContext.OutboxMessages.Where(m => m.Status == OutboxMessageStatus.Processed).ToListAsync());
    }

    private sealed record UnregisteredPoisonEvent(string Reason);

    [Fact]
    public async Task UnresolvableMessage_IsArchivedToDeadLetter_AndRemovedFromTheActiveTable()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();

        var poison = OutboxMessage.Create(new UnregisteredPoisonEvent("never registered with EventTypeRegistry"), DateTime.UtcNow);
        dbContext.OutboxMessages.Add(poison);
        await dbContext.SaveChangesAsync();

        await _factory.ProcessOutboxMessagesAsync();

        var stillInActiveTable = await dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == poison.Id);
        Assert.Null(stillInActiveTable);

        var deadLettered = await dbContext.DeadLetterEvents.FirstOrDefaultAsync(d => d.SourceEventId == poison.Id);
        Assert.NotNull(deadLettered);
        Assert.Contains("No registered event type", deadLettered!.FailureReason);
    }
}
