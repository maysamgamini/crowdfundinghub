namespace CrowdFunding.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// A CNCF CloudEvents v1.0 envelope wrapping an application event for delivery across a process
/// boundary (RabbitMQ, Kafka, ...). The in-process bus never constructs one of these — local
/// handlers receive <c>Data</c> directly — this only exists at the distributed-broker edge, so
/// an external consumer sees a standard, tool-agnostic envelope instead of a bespoke shape.
/// </summary>
public sealed record CloudEventEnvelope(
    string Id,
    string Source,
    string Type,
    DateTimeOffset Time,
    string DataContentType,
    string? TraceParent,
    object Data)
{
    public const string SpecVersion = "1.0";
}
