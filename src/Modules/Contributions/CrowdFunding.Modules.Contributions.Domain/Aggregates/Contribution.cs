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

    private Contribution()
    {
    }

    private Contribution(
        Guid id,
        Guid campaignId,
        Guid contributorId,
        Money money,
        ContributionStatus status,
        DateTime createdAtUtc)
    {
        Id = id;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Money = money;
        Status = status;
        CreatedAtUtc = createdAtUtc;
    }

    public static Contribution Create(
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        DateTime createdAtUtc)
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
            createdAtUtc);
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
        AddDomainEvent(new ContributionPaymentConfirmedDomainEvent(Id, CampaignId, ContributorId, Money.Amount, Money.Currency));
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
        AddDomainEvent(new ContributionRefundedDomainEvent(Id, CampaignId, ContributorId, Money.Amount, Money.Currency));
    }
}
