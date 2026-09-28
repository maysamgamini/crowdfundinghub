namespace CrowdFunding.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Names of the <see cref="System.Diagnostics.ActivitySource"/>s this codebase creates. Hosts pass
/// <see cref="ActivitySourceNames"/> to the OpenTelemetry SDK (<c>AddSource</c>); an activity
/// source nobody subscribes to produces no spans at all.
/// </summary>
public static class CrowdFundingTelemetry
{
    /// <summary>
    /// The outbox dispatch span (<c>Outbox.Process {EventType}</c>), parented on the W3C trace
    /// context captured when the originating request wrote the outbox row.
    /// </summary>
    public const string OutboxActivitySourceName = "CrowdFunding.Outbox";

    /// <summary>
    /// Every activity source name to register with the tracer provider.
    /// </summary>
    public static readonly string[] ActivitySourceNames = [OutboxActivitySourceName];
}
