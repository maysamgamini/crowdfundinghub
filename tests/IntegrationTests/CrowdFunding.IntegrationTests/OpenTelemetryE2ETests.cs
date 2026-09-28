using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace CrowdFunding.IntegrationTests;

/// <summary>
/// The OpenTelemetry SDK is registered, subscribes to this codebase's own activity sources, and a
/// trace that starts at an HTTP request continues through the outbox dispatch of the event that
/// request raised.
/// </summary>
[Collection(CrowdFundingApiCollection.Name)]
public sealed class OpenTelemetryE2ETests
{
    private readonly CrowdFundingApiFactory _factory;

    public OpenTelemetryE2ETests(CrowdFundingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void TracerProvider_ShouldBeRegistered_AndListenToTheOutboxActivitySource()
    {
        Assert.NotNull(_factory.Services.GetService<TracerProvider>());

        using var outboxSource = new ActivitySource(CrowdFundingTelemetry.OutboxActivitySourceName);
        Assert.True(outboxSource.HasListeners());
    }

    [Fact]
    public async Task OutboxDispatchSpan_ShouldContinueTheTraceOfTheRequestThatWroteTheEvent()
    {
        var outboxSpans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CrowdFundingTelemetry.OutboxActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = outboxSpans.Add,
        };
        ActivitySource.AddActivityListener(listener);

        using var client = _factory.CreateClient();
        var (_, token) = await client.RegisterAndLoginAsync(ApiTestExtensions.UniqueEmail("otel-trace"));
        client.SetBearerToken(token);

        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/campaigns")
        {
            Content = JsonContent.Create(new CreateCampaignRequest(
                "Traced Campaign",
                "A story that is definitely longer than twenty characters to satisfy validation rules.",
                "Technology",
                5000m,
                "USD",
                DateTime.UtcNow.AddDays(30))),
        };
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await _factory.ProcessOutboxMessagesAsync();

        Assert.Contains(outboxSpans, span =>
            span.TraceId == traceId && span.DisplayName == "Outbox.Process CampaignCreatedApplicationEvent");
    }
}
