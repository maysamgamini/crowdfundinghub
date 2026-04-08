using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Contributions.Domain.Enums;
using CrowdFunding.Modules.Contributions.Domain.Events;

namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

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
        AddDomainEvent(new ContributionPaymentConfirmedDomainEvent(Id, CampaignId, Money.Amount, Money.Currency));
    }

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
}
