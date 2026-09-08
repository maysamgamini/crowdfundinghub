using System.Text.Json.Serialization;

namespace CrowdFunding.BuildingBlocks.Application.Metering;

/// <summary>
/// A usage/billing event in CloudEvents v1.0 JSON format (https://cloudevents.io), the wire
/// format <see cref="IUsageMeteringClient"/> ingests. See improvement.md §4.2 for the specific
/// event schemas this platform emits (currently: crowdfunding.pledge.confirmed).
/// </summary>
public sealed record CloudEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("time")] DateTimeOffset Time,
    [property: JsonPropertyName("data")] object Data)
{
    [JsonPropertyName("specversion")]
    public string SpecVersion { get; init; } = "1.0";

    [JsonPropertyName("datacontenttype")]
    public string DataContentType { get; init; } = "application/json";
}
