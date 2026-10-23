using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.API.Migrations;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The closing fence, end to end. A backer pledged while the campaign was live, but their payment was
/// only confirmed after the campaign was cancelled. The campaign must not count it, nothing may land in
/// the dead-letter table, and the backer must be refunded.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class LateConfirmationRefundE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public LateConfirmationRefundE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task APaymentConfirmedAfterTheCampaignEnded_IsRefunded_NotCounted_AndNotDeadLettered()
    {
        using var client = _factory.CreateClient();
        var (_, creatorToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("late-creator"), "Creator");
        client.SetBearerToken(creatorToken);
        var created = await (await client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest(
            "Late Confirmation Campaign",
            "A story that is definitely longer than twenty characters, describing this campaign's purpose in full.",
            "Community", 10_000m, "USD", DateTime.UtcNow.AddDays(30)))).Content.ReadFromJsonAsync<CreateCampaignResponse>();
        var campaignId = created!.CampaignId;
        await _factory.ProcessOutboxMessagesAsync();

        var adminEmail = ApiTestExtensions.UniqueEmail("late-admin");
        await AdminSeeder.RunAsync(_factory.Services, adminEmail, ApiTestExtensions.DefaultPassword, "Admin");
        var adminToken = await client.LoginUserAsync(adminEmail);
        client.SetBearerToken(adminToken);
        (await client.PostAsJsonAsync($"/api/moderation/reviews/{campaignId}/approve", new ReviewCampaignRequest(Notes: null))).EnsureSuccessStatusCode();
        client.SetBearerToken(creatorToken);
        (await client.PostAsync($"/api/campaigns/{campaignId}/publish", content: null)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        var (_, backerToken) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("late-backer"), "Backer");
        client.SetBearerToken(backerToken);
        var pledge = await (await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/contributions", new MakeContributionRequest(100m, "USD")))
            .Content.ReadFromJsonAsync<MakeContributionResponse>();

        // The campaign ends.
        client.SetBearerToken(creatorToken);
        (await client.PostAsync($"/api/campaigns/{campaignId}/cancel", content: null)).EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        // The payment confirmation was already on its way when the campaign ended, so Contributions had
        // accepted it. Over HTTP that window is now only milliseconds wide (the wake-up signal delivers the
        // cancellation at once, and the confirm endpoint refuses an ended campaign), so the confirmation is
        // applied here through the module's own services, exactly as the endpoint would have done it.
        await using (var confirmScope = _factory.Services.CreateAsyncScope())
        {
            var repository = confirmScope.ServiceProvider.GetRequiredService<IContributionRepository>();
            var executor = confirmScope.ServiceProvider.GetRequiredService<IContributionTransactionExecutor>();
            var pending = await repository.GetByIdAsync(pledge!.ContributionId, CancellationToken.None);
            await executor.ExecuteAsync(async ct =>
            {
                pending!.ConfirmPayment("late-payment-ref", DateTime.UtcNow);
                await repository.UpdateAsync(pending, ct);
            }, CancellationToken.None);
        }

        // Confirmed -> Campaigns refuses it -> LateContributionRejected -> Contributions refunds it.
        await _factory.ProcessOutboxMessagesAsync();
        await _factory.ProcessOutboxMessagesAsync();

        var after = await client.GetFromJsonAsync<GetContributionByIdResponse>($"/api/campaigns/{campaignId}/contributions/{pledge.ContributionId}");
        Assert.Equal("Refunded", after!.Status);

        await using var scope = _factory.Services.CreateAsyncScope();
        var campaigns = scope.ServiceProvider.GetRequiredService<CampaignsDbContext>();
        var contributions = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        var id = pledge.ContributionId.ToString();

        // Before the fix the credit threw, the confirmation event went back to Pending with a failed attempt
        // and, after five of them, to the dead-letter table, with the backer never refunded.
        Assert.False(await contributions.OutboxMessages.AnyAsync(m => m.Payload.Contains(id) && m.Attempts > 0));
        Assert.False(await campaigns.OutboxMessages.AnyAsync(m => m.Payload.Contains(id) && m.Attempts > 0));
        Assert.False(await contributions.DeadLetterEvents.AnyAsync(d => d.Payload.Contains(id)));
        var campaign = await campaigns.Campaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        Assert.Equal(0m, campaign.RaisedAmount.Amount);
    }
}
