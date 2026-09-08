using System.Text.Json.Serialization;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Metering;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

namespace CrowdFunding.Modules.Contributions.Application.Features.Metering;

/// <summary>
/// Projects each confirmed pledge into a <c>crowdfunding.pledge.confirmed</c> CloudEvent for
/// OpenMeter (improvement.md §4.1/§4.2) — platform success fees, payment processing fees, and
/// gross transaction volume all need to be metered per confirmed contribution.
///
/// Runs as an ordinary <see cref="IEventHandler{TEvent}"/> alongside the existing
/// ContributionPaymentConfirmedApplicationEventHandler in the Campaigns module: both are invoked
/// from the same outbox dispatch, so if this pledge is redelivered (at-least-once), OpenMeter's
/// own idempotency key (<see cref="BuildCloudEventId"/>, deterministic per ContributionId) makes
/// re-ingestion a safe no-op, mirroring the ledger-based idempotency already in place for the
/// balance update itself.
/// </summary>
public sealed class PledgeConfirmedMeteringEventHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    private readonly IUsageMeteringClient _meteringClient;
    private readonly OpenMeterFeeOptions _feeOptions;

    public PledgeConfirmedMeteringEventHandler(IUsageMeteringClient meteringClient, OpenMeterFeeOptions feeOptions)
    {
        _meteringClient = meteringClient;
        _feeOptions = feeOptions;
    }

    public Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var grossAmountCents = (long)Math.Round(notification.Amount * 100m, MidpointRounding.AwayFromZero);
        var platformFeeCents = grossAmountCents * _feeOptions.PlatformFeeBps / 10_000;
        var processingFeeCents = _feeOptions.ProcessingFeeFixedCents + (grossAmountCents * _feeOptions.ProcessingFeeBps / 10_000);
        var netAmountCents = grossAmountCents - platformFeeCents - processingFeeCents;

        var cloudEvent = new CloudEvent(
            Id: BuildCloudEventId(notification.ContributionId),
            Source: "/modules/contributions",
            Type: "crowdfunding.pledge.confirmed",
            Subject: $"campaign_{notification.CampaignId}",
            Time: DateTimeOffset.UtcNow,
            Data: new PledgeConfirmedData(
                notification.ContributionId,
                notification.CampaignId,
                grossAmountCents,
                platformFeeCents,
                processingFeeCents,
                netAmountCents,
                notification.Currency));

        // IUsageMeteringClient implementations do not throw on ingestion failure (see its
        // remarks) — metering must never block or retry the pledge-confirmation pipeline.
        return _meteringClient.IngestAsync(cloudEvent, cancellationToken);
    }

    private static string BuildCloudEventId(Guid contributionId) => $"pledge_evt_{contributionId}";

    private sealed record PledgeConfirmedData(
        [property: JsonPropertyName("contribution_id")] Guid ContributionId,
        [property: JsonPropertyName("campaign_id")] Guid CampaignId,
        [property: JsonPropertyName("gross_amount_cents")] long GrossAmountCents,
        [property: JsonPropertyName("platform_fee_cents")] long PlatformFeeCents,
        [property: JsonPropertyName("processing_fee_cents")] long ProcessingFeeCents,
        [property: JsonPropertyName("net_amount_cents")] long NetAmountCents,
        [property: JsonPropertyName("currency")] string Currency);
}
