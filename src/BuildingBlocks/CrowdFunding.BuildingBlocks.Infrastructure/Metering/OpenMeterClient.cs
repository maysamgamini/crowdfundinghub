using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrowdFunding.BuildingBlocks.Application.Metering;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Metering;

/// <summary>
/// Ingests CloudEvents into OpenMeter via <c>POST /api/v1/events</c>
/// (https://openmeter.io/docs/api#tag/events/operation/ingestEvents). Registered as a typed
/// HttpClient with the standard .NET resilience handler (retry + circuit breaker + timeout), so
/// transient OpenMeter failures are retried automatically without this class needing its own
/// Polly policy.
/// </summary>
public sealed class OpenMeterClient : IUsageMeteringClient
{
    private const string EventsPath = "/api/v1/events";
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeterClient> _logger;

    public OpenMeterClient(HttpClient httpClient, ILogger<OpenMeterClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task IngestAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, EventsPath)
            {
                Content = JsonContent.Create(cloudEvent),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/cloudevents+json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            // Deliberately swallowed rather than rethrown: metering must never turn into a
            // reason to block or retry the business event pipeline that raised this CloudEvent
            // (see IUsageMeteringClient's remarks). Callers that need stronger delivery
            // guarantees should catch at their own layer instead of relying on this not to throw.
            _logger.LogWarning(
                exception,
                "Failed to ingest CloudEvent {EventId} (type={EventType}) into OpenMeter.",
                cloudEvent.Id, cloudEvent.Type);
        }
    }
}
