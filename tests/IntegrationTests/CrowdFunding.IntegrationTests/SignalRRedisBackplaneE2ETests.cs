using System.Text.Json;
using CrowdFunding.API.RealTime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// TICKET-048 acceptance criterion 2: "A test spinning up two instances sharing a Redis
/// Testcontainer verifies that broadcasting a pledge from Instance 2 delivers the message to a
/// client connected to Instance 1." Instance 1 is <see cref="CrowdFundingApiFactory"/>'s own
/// already-running host (simulating one pod behind a load balancer); Instance 2 is a second,
/// independently built <see cref="WebApplicationFactory{TEntryPoint}"/> pointed at the exact same
/// Redis connection string (simulating a second pod) — proving the broadcast crosses
/// process/container boundaries via Redis Pub/Sub (<c>AddStackExchangeRedis</c>), not just
/// SignalR's own in-memory, single-instance group table.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class SignalRRedisBackplaneE2ETests
{
    private readonly CrowdFundingApiFactory _instanceOne;

    public SignalRRedisBackplaneE2ETests(CrowdFundingApiFactory instanceOne)
    {
        _instanceOne = instanceOne;
    }

    [Fact]
    public async Task BroadcastFromInstanceTwo_ShouldReachClientConnectedToInstanceOne()
    {
        var campaignId = Guid.NewGuid();
        var pledgeReceivedTcs = new TaskCompletionSource<(Guid CampaignId, decimal RaisedAmount, string Currency)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // A client connects only to Instance 1 — it never talks to Instance 2 at all. If it
        // observes the broadcast raised on Instance 2 below, that message can only have arrived
        // via the shared Redis backplane.
        await using var instanceOneClient = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_instanceOne.Server.BaseAddress, "/hubs/campaigns"),
                HttpTransportType.LongPolling, // TestServer has no real socket to negotiate WebSockets over.
                options => options.HttpMessageHandlerFactory = _ => _instanceOne.Server.CreateHandler())
            .Build();

        instanceOneClient.On<JsonElement>("PledgeReceived", payload =>
        {
            var receivedCampaignId = payload.GetProperty("campaignId").GetGuid();
            var receivedRaisedAmount = payload.GetProperty("raisedAmount").GetDecimal();
            var receivedCurrency = payload.GetProperty("currency").GetString()!;
            pledgeReceivedTcs.TrySetResult((receivedCampaignId, receivedRaisedAmount, receivedCurrency));
        });

        await instanceOneClient.StartAsync();
        await instanceOneClient.InvokeAsync("JoinCampaign", campaignId);

        await using var instanceTwo = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseContentRoot(AppContext.BaseDirectory);
        });

        // Never connects a client — this instance only ever publishes, exactly like a pod that
        // processed the HTTP request/outbox event but holds no WebSocket connection for this
        // campaign's group itself.
        var instanceTwoHubContext = instanceTwo.Services.GetRequiredService<IHubContext<CampaignHub>>();

        await instanceTwoHubContext.Clients
            .Group(CampaignHub.GroupName(campaignId))
            .SendAsync("PledgeReceived", new { campaignId, raisedAmount = 250m, currency = "USD" });

        var received = await pledgeReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(campaignId, received.CampaignId);
        Assert.Equal(250m, received.RaisedAmount);
        Assert.Equal("USD", received.Currency);

        await instanceOneClient.InvokeAsync("LeaveCampaign", campaignId);
    }
}
