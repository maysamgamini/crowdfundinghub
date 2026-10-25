using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Pledges.Domain.Enums;
using CrowdFunding.Modules.Pledges.Domain.Events;

namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// Represents the pledge aggregate root and enforces payment-state transitions.
/// </summary>
public sealed class Pledge : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid BackerId { get; private set; }
    public Money Money { get; private set; } = null!;
    public PledgeStatus Status { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>The external payment service provider's reference for this pledge (e.g. a
    /// Stripe PaymentIntent id) — the correlation key an inbound payment webhook reconciles
    /// against. See TICKET-033.</summary>
    public string? ExternalPaymentIntentId { get; private set; }

    /// <summary>Which external payment gateway <see cref="ExternalPaymentIntentId"/> belongs to
    /// (e.g. "Stripe", "Mock") — a pledge could in principle move providers if the platform
    /// migrates gateways mid-lifecycle, so this travels with the reference rather than being a
    /// platform-wide constant.</summary>
    public string? PaymentGateway { get; private set; }

    /// <summary>The reward tier slot reservation (Campaigns module) this pledge is checking out
    /// against, if the backer selected a perk — an opaque correlation id from Pledges'
    /// point of view (see TICKET-043). Carried on <see cref="Events.PledgePaymentConfirmedDomainEvent"/>
    /// so Campaigns can convert the reservation into a claim once payment is confirmed, and left
    /// untouched on failure/refund so the reservation scavenger remains the sole path back to
    /// available inventory for an abandoned or declined checkout.</summary>
    public Guid? RewardTierReservationId { get; private set; }

    /// <summary>The payment provider's id for the refund of this pledge, once it has been refunded through the provider.</summary>
    public string? RefundReference { get; private set; }

    /// <summary>How many refund attempts the provider has answered with a failure. The next attempt gets the next number, and with it a new idempotency key.</summary>
    public int RefundFailures { get; private set; }

    /// <summary>When the next refund attempt may be made after the provider refused one. Empty means now.</summary>
    public DateTime? NextRefundAtUtc { get; private set; }

    /// <summary>The saved card the provider returned when the backer finished setup. Only a reference: card numbers never reach this system.</summary>
    public string? PaymentMethodReference { get; private set; }

    /// <summary>How many charge attempts have been started at the deadline. The attempt in flight is this number.</summary>
    public int ChargeAttempts { get; private set; }

    /// <summary>When the pledge is next due to be charged (set when the campaign is funded, and after a declined attempt).</summary>
    public DateTime? NextChargeAtUtc { get; private set; }

    /// <summary>Until when a worker holds the charge attempt. After this another worker may resume the SAME attempt.</summary>
    public DateTime? ChargeLeaseUntilUtc { get; private set; }

    /// <summary>The provider's reason for the most recent failed refund attempt.</summary>
    public string? LastRefundFailure { get; private set; }

    /// <summary>The client-chosen key that makes creating this pledge safe to retry. A second
    /// request from the same backer with the same key returns this pledge instead of creating
    /// another. Null when the client sent no key (the old behaviour: every request is a new pledge).</summary>
    public string? IdempotencyKey { get; private set; }

    public const int MaxIdempotencyKeyLength = 100;

    private Pledge()
    {
    }

    private Pledge(
        Guid id,
        Guid campaignId,
        Guid backerId,
        Money money,
        PledgeStatus status,
        DateTime createdAtUtc,
        Guid? rewardTierReservationId,
        string? idempotencyKey)
    {
        Id = id;
        CampaignId = campaignId;
        BackerId = backerId;
        Money = money;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        RewardTierReservationId = rewardTierReservationId;
        IdempotencyKey = idempotencyKey;
    }

    public static Pledge Create(
        Guid campaignId,
        Guid backerId,
        decimal amount,
        string currency,
        DateTime createdAtUtc,
        Guid? rewardTierReservationId = null,
        string? idempotencyKey = null)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (backerId == Guid.Empty)
        {
            throw new ArgumentException("BackerId is required.", nameof(backerId));
        }

        if (idempotencyKey is not null && (idempotencyKey.Length == 0 || idempotencyKey.Length > MaxIdempotencyKeyLength))
        {
            throw new ArgumentException($"The idempotency key must be 1 to {MaxIdempotencyKeyLength} characters.", nameof(idempotencyKey));
        }

        var money = new Money(amount, currency);

        if (money.Amount <= 0)
        {
            throw new ArgumentException("Pledge amount must be greater than zero.", nameof(amount));
        }

        return new Pledge(
            Guid.NewGuid(),
            campaignId,
            backerId,
            money,
            PledgeStatus.Pending,
            createdAtUtc,
            rewardTierReservationId,
            idempotencyKey);
    }

    /// <summary>
    /// Records the external payment gateway's reference for this pledge, obtained when the
    /// gateway's payment-intent/charge object was created. Settable only while the pledge
    /// is still awaiting payment — once a terminal state is reached, the reference that produced
    /// it is immutable history.
    /// </summary>
    public void AttachPaymentIntent(string externalPaymentIntentId, string paymentGateway)
    {
        if (Status != PledgeStatus.Pending)
        {
            throw new InvalidOperationException("A payment intent can only be attached while the pledge is pending.");
        }

        if (string.IsNullOrWhiteSpace(externalPaymentIntentId))
        {
            throw new ArgumentException("External payment intent id is required.", nameof(externalPaymentIntentId));
        }

        ExternalPaymentIntentId = externalPaymentIntentId.Trim();
        PaymentGateway = paymentGateway;
    }

    /// <summary>
    /// Transitions the pledge status to Confirmed upon successful payment.
    /// </summary>
    /// <param name="paymentReference">The external payment processor transaction reference.</param>
    /// <param name="processedAtUtc">The UTC timestamp when payment was confirmed.</param>
    public void ConfirmPayment(string paymentReference, DateTime processedAtUtc)
    {
        if (Status != PledgeStatus.Pending)
        {
            throw new InvalidOperationException("Only pending pledges can be confirmed.");
        }

        if (string.IsNullOrWhiteSpace(paymentReference))
        {
            throw new ArgumentException("Payment reference is required.", nameof(paymentReference));
        }

        Status = PledgeStatus.Succeeded;
        PaymentReference = paymentReference.Trim();
        FailureReason = null;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new PledgePaymentConfirmedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, RewardTierReservationId));
    }

    /// <summary>
    /// Transitions the pledge status to Failed upon payment rejection.
    /// </summary>
    /// <param name="failureReason">The reason for payment failure.</param>
    /// <param name="processedAtUtc">The UTC timestamp when failure occurred.</param>
    public void FailPayment(string failureReason, DateTime processedAtUtc)
    {
        if (Status != PledgeStatus.Pending)
        {
            throw new InvalidOperationException("Only pending pledges can be failed.");
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));
        }

        Status = PledgeStatus.Failed;
        PaymentReference = null;
        FailureReason = failureReason.Trim();
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new PledgePaymentFailedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, FailureReason, RewardTierReservationId));
    }

    /// <summary>
    /// Refunds a succeeded pledge as a compensating action when its campaign fails or is
    /// cancelled after payment was already confirmed. Only ever transitions FROM Succeeded — this
    /// makes the refund saga naturally idempotent: re-delivering the triggering
    /// CampaignFailedApplicationEvent/CampaignCancelledApplicationEvent finds the pledge
    /// already Refunded and is filtered out by <c>IPledgeRepository.GetSucceededByCampaignIdAsync</c>
    /// rather than throwing or double-refunding.
    /// </summary>
    /// <summary>
    /// Records that the provider answered a refund attempt with a failure. The next attempt is a new one
    /// (the number goes up, so it gets a new key); a timeout is not recorded here, because a timeout is not an answer.
    /// </summary>
    /// <summary>The backer finished setup: the card is saved, nothing is charged, and the pledge now counts.</summary>
    public PledgeActivation ActivatePledge(string paymentMethodReference, DateTime processedAtUtc)
    {
        if (Status != PledgeStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending pledge can be activated.");
        }

        if (string.IsNullOrWhiteSpace(paymentMethodReference))
        {
            throw new ArgumentException("A saved payment method reference is required.", nameof(paymentMethodReference));
        }

        Status = PledgeStatus.Active;
        PaymentMethodReference = paymentMethodReference.Trim();
        FailureReason = null;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new PledgeActivatedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, RewardTierReservationId));
        return new PledgeActivation(Id, PaymentMethodReference, processedAtUtc);
    }

    /// <summary>The campaign ended without being funded, or was cancelled: the promise ends and nothing is charged.</summary>
    public void ReleasePledge(DateTime processedAtUtc)
    {
        if (Status is not (PledgeStatus.Pending or PledgeStatus.Active))
        {
            throw new InvalidOperationException($"Only a pending or active pledge can be released. Current status: '{Status}'.");
        }

        Status = PledgeStatus.Released;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new PledgeReleasedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, RewardTierReservationId));
    }

    /// <summary>The campaign was funded: this pledge now waits to be charged.</summary>
    public void QueueForCharge(DateTime nowUtc)
    {
        if (Status != PledgeStatus.Active)
        {
            throw new InvalidOperationException($"Only an active pledge can be queued for charging. Current status: '{Status}'.");
        }

        Status = PledgeStatus.ToCharge;
        NextChargeAtUtc = nowUtc;
    }

    /// <summary>
    /// The charge succeeded: the money was taken. From here on the pledge is <c>Succeeded</c>, like any other
    /// paid pledge, and the same payment-confirmed event goes out (receipt, platform fee, creator webhooks).
    /// </summary>
    public void RecordChargeSucceeded(string providerPaymentId, DateTime processedAtUtc)
    {
        if (Status != PledgeStatus.Charging)
        {
            throw new InvalidOperationException($"Only a pledge being charged can record a successful charge. Current status: '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            throw new ArgumentException("The provider's payment id is required.", nameof(providerPaymentId));
        }

        Status = PledgeStatus.Succeeded;
        PaymentReference = providerPaymentId.Trim();
        FailureReason = null;
        NextChargeAtUtc = null;
        ChargeLeaseUntilUtc = null;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new PledgePaymentConfirmedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, RewardTierReservationId));
    }

    /// <summary>
    /// The provider answered a charge attempt with a decline. Below <paramref name="maxAttempts"/> the pledge goes
    /// back to waiting, to be tried again later as the next numbered attempt; at the limit it fails for good.
    /// </summary>
    public void RecordChargeDeclined(string declineCode, int maxAttempts, TimeSpan retryDelay, DateTime nowUtc)
    {
        if (Status != PledgeStatus.Charging)
        {
            throw new InvalidOperationException($"Only a pledge being charged can record a declined charge. Current status: '{Status}'.");
        }

        ChargeLeaseUntilUtc = null;

        if (ChargeAttempts >= maxAttempts)
        {
            Status = PledgeStatus.Failed;
            FailureReason = $"The charge was declined {ChargeAttempts} times. Last reason: {declineCode}.";
            NextChargeAtUtc = null;
            ProcessedAtUtc = nowUtc;
            AddDomainEvent(new PledgePaymentFailedDomainEvent(
                Id, CampaignId, BackerId, Money.Amount, Money.Currency, FailureReason, RewardTierReservationId));
            return;
        }

        Status = PledgeStatus.ToCharge;
        FailureReason = declineCode;
        NextChargeAtUtc = nowUtc.Add(retryDelay);
    }

    public void RecordRefundFailure(string failureCode, DateTime? nextRefundAtUtc = null)
    {
        if (Status != PledgeStatus.Succeeded)
        {
            throw new InvalidOperationException($"Cannot record a refund failure for a pledge with status '{Status}'.");
        }

        RefundFailures++;
        LastRefundFailure = failureCode;
        NextRefundAtUtc = nextRefundAtUtc;
    }

    public void Refund(DateTime processedAtUtc, string? refundReference = null)
    {
        if (Status != PledgeStatus.Succeeded)
        {
            throw new InvalidOperationException($"Cannot refund pledge with status '{Status}'.");
        }

        Status = PledgeStatus.Refunded;
        ProcessedAtUtc = processedAtUtc;
        RefundReference = refundReference;
        NextRefundAtUtc = null;
        AddDomainEvent(new PledgeRefundedDomainEvent(
            Id, CampaignId, BackerId, Money.Amount, Money.Currency, RewardTierReservationId));
    }
}
