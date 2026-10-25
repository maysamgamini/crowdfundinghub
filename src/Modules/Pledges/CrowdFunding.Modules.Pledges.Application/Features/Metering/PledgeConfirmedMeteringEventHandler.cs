using System.Text.Json.Serialization;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Metering;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentConfirmed;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Features.Metering;

/// <summary>
/// Projects each confirmed pledge into a <c>crowdfunding.pledge.confirmed</c> CloudEvent for
/// OpenMeter (improvement.md §4.1/§4.2) — platform success fees, payment processing fees, and
/// gross transaction volume all need to be metered per confirmed pledge.
///
/// Runs as an ordinary <see cref="IEventHandler{TEvent}"/> alongside the existing
/// PledgePaymentConfirmedApplicationEventHandler in the Campaigns module: both are invoked
/// from the same outbox dispatch, so if this pledge is redelivered (at-least-once), OpenMeter's
/// own idempotency key (<see cref="BuildCloudEventId"/>, deterministic per PledgeId) makes
/// re-ingestion a safe no-op, mirroring the ledger-based idempotency already in place for the
/// balance update itself.
/// </summary>
public sealed class PledgeConfirmedMeteringEventHandler : IEventHandler<PledgePaymentConfirmedApplicationEvent>
{
    private readonly IUsageMeteringClient _meteringClient;
    private readonly OpenMeterFeeOptions _feeOptions;

    public PledgeConfirmedMeteringEventHandler(IUsageMeteringClient meteringClient, OpenMeterFeeOptions feeOptions)
    {
        _meteringClient = meteringClient;
        _feeOptions = feeOptions;
    }

    /// <inheritdoc/>
    public Task Handle(PledgePaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        var grossAmountCents = (long)Math.Round(notification.Amount * 100m, MidpointRounding.AwayFromZero);
        // The same fee a payout adds up, so the metered fees of a campaign sum to its payout fee.
        var platformFeeCents = (long)Math.Round(PlatformFee.ForPledge(notification.Amount, _feeOptions.PlatformFeeBps) * 100m, MidpointRounding.AwayFromZero);
        var processingFeeCents = _feeOptions.ProcessingFeeFixedCents + (grossAmountCents * _feeOptions.ProcessingFeeBps / 10_000);
        var netAmountCents = grossAmountCents - platformFeeCents - processingFeeCents;

        var cloudEvent = new CloudEvent(
            Id: BuildCloudEventId(notification.PledgeId),
            Source: "/modules/pledges",
            Type: "crowdfunding.pledge.confirmed",
            Subject: $"campaign_{notification.CampaignId}",
            Time: DateTimeOffset.UtcNow,
            Data: new PledgeConfirmedData(
                notification.PledgeId,
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

    private static string BuildCloudEventId(Guid pledgeId) => $"pledge_evt_{pledgeId}";

    private sealed record PledgeConfirmedData(
        [property: JsonPropertyName("pledge_id")] Guid PledgeId,
        [property: JsonPropertyName("campaign_id")] Guid CampaignId,
        [property: JsonPropertyName("gross_amount_cents")] long GrossAmountCents,
        [property: JsonPropertyName("platform_fee_cents")] long PlatformFeeCents,
        [property: JsonPropertyName("processing_fee_cents")] long ProcessingFeeCents,
        [property: JsonPropertyName("net_amount_cents")] long NetAmountCents,
        [property: JsonPropertyName("currency")] string Currency);
}
