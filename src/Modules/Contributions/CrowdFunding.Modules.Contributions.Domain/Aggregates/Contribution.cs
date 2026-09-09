using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Domain.Events;

namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

/// <summary>
/// Represents the contribution aggregate root and enforces payment-state transitions.
/// </summary>
public sealed class Contribution : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ContributorId { get; private set; }
    public Money Money { get; private set; } = null!;
    public ContributionStatus Status { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>The external payment service provider's reference for this pledge (e.g. a
    /// Stripe PaymentIntent id) — the correlation key an inbound payment webhook reconciles
    /// against. See TICKET-033.</summary>
    public string? ExternalPaymentIntentId { get; private set; }

    /// <summary>Which external payment gateway <see cref="ExternalPaymentIntentId"/> belongs to
    /// (e.g. "Stripe", "Mock") — a contribution could in principle move providers if the platform
    /// migrates gateways mid-lifecycle, so this travels with the reference rather than being a
    /// platform-wide constant.</summary>
    public string? PaymentGateway { get; private set; }

    /// <summary>The reward tier slot reservation (Campaigns module) this pledge is checking out
    /// against, if the backer selected a perk — an opaque correlation id from Contributions'
    /// point of view (see TICKET-043). Carried on <see cref="Events.ContributionPaymentConfirmedDomainEvent"/>
    /// so Campaigns can convert the reservation into a claim once payment is confirmed, and left
    /// untouched on failure/refund so the reservation scavenger remains the sole path back to
    /// available inventory for an abandoned or declined checkout.</summary>
    public Guid? RewardTierReservationId { get; private set; }

    private Contribution()
    {
    }

    private Contribution(
        Guid id,
        Guid campaignId,
        Guid contributorId,
        Money money,
        ContributionStatus status,
        DateTime createdAtUtc,
        Guid? rewardTierReservationId)
    {
        Id = id;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Money = money;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        RewardTierReservationId = rewardTierReservationId;
    }

    public static Contribution Create(
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        DateTime createdAtUtc,
        Guid? rewardTierReservationId = null)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        if (contributorId == Guid.Empty)
        {
            throw new ArgumentException("ContributorId is required.", nameof(contributorId));
        }

        var money = new Money(amount, currency);

        if (money.Amount <= 0)
        {
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));
        }

        return new Contribution(
            Guid.NewGuid(),
            campaignId,
            contributorId,
            money,
            ContributionStatus.Pending,
            createdAtUtc,
            rewardTierReservationId);
    }

    /// <summary>
    /// Records the external payment gateway's reference for this pledge, obtained when the
    /// gateway's payment-intent/charge object was created. Settable only while the contribution
    /// is still awaiting payment — once a terminal state is reached, the reference that produced
    /// it is immutable history.
    /// </summary>
    public void AttachPaymentIntent(string externalPaymentIntentId, string paymentGateway)
    {
        if (Status != ContributionStatus.Pending)
        {
            throw new InvalidOperationException("A payment intent can only be attached while the contribution is pending.");
        }

        if (string.IsNullOrWhiteSpace(externalPaymentIntentId))
        {
            throw new ArgumentException("External payment intent id is required.", nameof(externalPaymentIntentId));
        }

        ExternalPaymentIntentId = externalPaymentIntentId.Trim();
        PaymentGateway = paymentGateway;
    }

    /// <summary>
    /// Transitions the contribution status to Confirmed upon successful payment.
    /// </summary>
    /// <param name="paymentReference">The external payment processor transaction reference.</param>
    /// <param name="processedAtUtc">The UTC timestamp when payment was confirmed.</param>
    public void ConfirmPayment(string paymentReference, DateTime processedAtUtc)
    {
        if (Status != ContributionStatus.Pending)
        {
            throw new InvalidOperationException("Only pending contributions can be confirmed.");
        }

        if (string.IsNullOrWhiteSpace(paymentReference))
        {
            throw new ArgumentException("Payment reference is required.", nameof(paymentReference));
        }

        Status = ContributionStatus.Succeeded;
        PaymentReference = paymentReference.Trim();
        FailureReason = null;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new ContributionPaymentConfirmedDomainEvent(
            Id, CampaignId, ContributorId, Money.Amount, Money.Currency, RewardTierReservationId));
    }

    /// <summary>
    /// Transitions the contribution status to Failed upon payment rejection.
    /// </summary>
    /// <param name="failureReason">The reason for payment failure.</param>
    /// <param name="processedAtUtc">The UTC timestamp when failure occurred.</param>
    public void FailPayment(string failureReason, DateTime processedAtUtc)
    {
        if (Status != ContributionStatus.Pending)
        {
            throw new InvalidOperationException("Only pending contributions can be failed.");
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(failureReason));
        }

        Status = ContributionStatus.Failed;
        PaymentReference = null;
        FailureReason = failureReason.Trim();
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new ContributionPaymentFailedDomainEvent(
            Id, CampaignId, ContributorId, Money.Amount, Money.Currency, FailureReason, RewardTierReservationId));
    }

    /// <summary>
    /// Refunds a succeeded contribution as a compensating action when its campaign fails or is
    /// cancelled after payment was already confirmed. Only ever transitions FROM Succeeded — this
    /// makes the refund saga naturally idempotent: re-delivering the triggering
    /// CampaignFailedApplicationEvent/CampaignCancelledApplicationEvent finds the contribution
    /// already Refunded and is filtered out by <c>IContributionRepository.GetSucceededByCampaignIdAsync</c>
    /// rather than throwing or double-refunding.
    /// </summary>
    public void Refund(DateTime processedAtUtc)
    {
        if (Status != ContributionStatus.Succeeded)
        {
            throw new InvalidOperationException($"Cannot refund contribution with status '{Status}'.");
        }

        Status = ContributionStatus.Refunded;
        ProcessedAtUtc = processedAtUtc;
        AddDomainEvent(new ContributionRefundedDomainEvent(
            Id, CampaignId, ContributorId, Money.Amount, Money.Currency, RewardTierReservationId));
    }
}
