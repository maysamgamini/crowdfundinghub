namespace CrowdFunding.BuildingBlocks.Application.Metering;

/// <summary>
/// Ingests usage/billing CloudEvents into the metering backend (OpenMeter). Implementations
/// should treat ingestion failures as non-fatal to the caller's own transaction/event delivery —
/// metering is a revenue-reporting concern, not a financial-correctness one (see the priority
/// ordering in improvement.md's punch list: financial correctness > ... > revenue), so a metering
/// outage must never block or retry the business event pipeline it's observing.
/// </summary>
public interface IUsageMeteringClient
{
    Task IngestAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default);
}
