using System.Net;
using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Metering;
using CrowdFunding.BuildingBlocks.Infrastructure.Metering;
using CrowdFunding.Modules.Contributions.Application.Features.Metering;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrowdFunding.UnitTests;

public sealed class OpenMeterClientTests
{
    [Fact]
    public async Task IngestAsync_ShouldPostCloudEventAsJson_WithCorrectContentType()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openmeter.test") };
        var client = new OpenMeterClient(httpClient, NullLogger<OpenMeterClient>.Instance);

        var cloudEvent = new CloudEvent(
            "pledge_evt_123", "/modules/contributions", "crowdfunding.pledge.confirmed",
            "campaign_abc", DateTimeOffset.UtcNow, new { amount = 100 });

        await client.IngestAsync(cloudEvent);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("/api/v1/events", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal("application/cloudevents+json", capturedRequest.Content!.Headers.ContentType!.MediaType);

        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("1.0", json.RootElement.GetProperty("specversion").GetString());
        Assert.Equal("pledge_evt_123", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("crowdfunding.pledge.confirmed", json.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task IngestAsync_ShouldNotThrow_WhenTheHttpCallFails()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openmeter.test") };
        var client = new OpenMeterClient(httpClient, NullLogger<OpenMeterClient>.Instance);

        var cloudEvent = new CloudEvent("id", "src", "type", "subject", DateTimeOffset.UtcNow, new { });

        // Must not throw: a metering outage must never propagate into the caller's pipeline.
        await client.IngestAsync(cloudEvent);
    }

    [Fact]
    public async Task IngestAsync_ShouldNotThrow_WhenOpenMeterReturnsAnErrorStatus()
    {
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://openmeter.test") };
        var client = new OpenMeterClient(httpClient, NullLogger<OpenMeterClient>.Instance);

        var cloudEvent = new CloudEvent("id", "src", "type", "subject", DateTimeOffset.UtcNow, new { });

        await client.IngestAsync(cloudEvent);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request);
    }
}

public sealed class PledgeConfirmedMeteringEventHandlerTests
{
    [Fact]
    public async Task Handle_ShouldComputeFeesAndForwardADeterministicCloudEvent()
    {
        var recordingClient = new RecordingUsageMeteringClient();
        var feeOptions = new OpenMeterFeeOptions
        {
            PlatformFeeBps = 500, // 5%
            ProcessingFeeFixedCents = 30,
            ProcessingFeeBps = 290, // 2.9%
        };
        var handler = new PledgeConfirmedMeteringEventHandler(recordingClient, feeOptions);

        var contributionId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        await handler.Handle(
            new ContributionPaymentConfirmedApplicationEvent(contributionId, campaignId, 100m, "USD"),
            CancellationToken.None);

        Assert.NotNull(recordingClient.LastCloudEvent);
        var cloudEvent = recordingClient.LastCloudEvent!;

        Assert.Equal($"pledge_evt_{contributionId}", cloudEvent.Id);
        Assert.Equal("crowdfunding.pledge.confirmed", cloudEvent.Type);
        Assert.Equal($"campaign_{campaignId}", cloudEvent.Subject);

        var json = JsonSerializer.Serialize(cloudEvent.Data);
        using var data = JsonDocument.Parse(json);
        Assert.Equal(10000, data.RootElement.GetProperty("gross_amount_cents").GetInt64());
        Assert.Equal(500, data.RootElement.GetProperty("platform_fee_cents").GetInt64());
        Assert.Equal(320, data.RootElement.GetProperty("processing_fee_cents").GetInt64());
        Assert.Equal(9180, data.RootElement.GetProperty("net_amount_cents").GetInt64());
    }

    [Fact]
    public async Task Handle_ShouldProduceTheSameCloudEventId_WhenTheSameContributionIsRedelivered()
    {
        var recordingClient = new RecordingUsageMeteringClient();
        var handler = new PledgeConfirmedMeteringEventHandler(recordingClient, new OpenMeterFeeOptions());
        var contributionId = Guid.NewGuid();
        var notification = new ContributionPaymentConfirmedApplicationEvent(contributionId, Guid.NewGuid(), 10m, "USD");

        await handler.Handle(notification, CancellationToken.None);
        var firstId = recordingClient.LastCloudEvent!.Id;

        await handler.Handle(notification, CancellationToken.None);
        var secondId = recordingClient.LastCloudEvent!.Id;

        // OpenMeter deduplicates by event id, so redelivery of the same contribution must
        // produce the same id rather than a fresh Guid each time.
        Assert.Equal(firstId, secondId);
    }

    private sealed class RecordingUsageMeteringClient : IUsageMeteringClient
    {
        public CloudEvent? LastCloudEvent { get; private set; }

        public Task IngestAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default)
        {
            LastCloudEvent = cloudEvent;
            return Task.CompletedTask;
        }
    }
}
