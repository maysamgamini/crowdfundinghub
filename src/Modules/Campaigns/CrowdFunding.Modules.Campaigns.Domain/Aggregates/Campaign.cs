using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.Exceptions;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Domain.Events;

namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

/// <summary>
/// Represents the campaign aggregate root and enforces campaign lifecycle rules.
/// </summary>
public sealed class Campaign : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Story { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public DateTime DeadlineUtc { get; private set; }
    public CampaignStatus Status { get; private set; }
    public Money GoalAmount { get; private set; } = null!;
    public Money RaisedAmount { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    private Campaign()
    {
    }

    private Campaign(
        Guid id,
        Guid ownerId,
        string title,
        string story,
        string category,
        Money goalAmount,
        DateTime deadlineUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        Story = story;
        Category = category;
        GoalAmount = goalAmount;
        RaisedAmount = Money.Zero(goalAmount.Currency);
        DeadlineUtc = deadlineUtc;
        CreatedAtUtc = createdAtUtc;
        Status = CampaignStatus.Draft;
    }

    public static Campaign Create(
        Guid ownerId,
        string title,
        string story,
        string category,
        Money goalAmount,
        DateTime deadlineUtc,
        DateTime createdAtUtc)
    {
        ValidateOwner(ownerId);
        ValidateTitle(title);
        ValidateStory(story);
        ValidateCategory(category);
        ValidateGoalAmount(goalAmount);
        ValidateDeadline(deadlineUtc, createdAtUtc);

        var campaign = new Campaign(
            Guid.NewGuid(),
            ownerId,
            title.Trim(),
            story.Trim(),
            category.Trim(),
            goalAmount,
            deadlineUtc,
            createdAtUtc);

        campaign.AddDomainEvent(new CampaignCreatedDomainEvent(
            campaign.Id, campaign.OwnerId, campaign.Title, campaign.GoalAmount.Currency, campaign.DeadlineUtc));

        return campaign;
    }

    /// <summary>
    /// Publishes the campaign from Draft to Active status.
    /// </summary>
    /// <param name="currentUtc">The current UTC timestamp.</param>
    public void Publish(DateTime currentUtc)
    {
        if (Status != CampaignStatus.Draft)
        {
            throw new DomainConflictException("Only draft campaigns can be published.");
        }

        if (DeadlineUtc <= currentUtc)
        {
            throw new DomainConflictException("Cannot publish a campaign with a past deadline.");
        }

        Status = CampaignStatus.Published;
        AddDomainEvent(new CampaignPublishedDomainEvent(Id, OwnerId));
    }

    /// <summary>
    /// Cancels the campaign.
    /// </summary>
    public void Cancel()
    {
        if (Status == CampaignStatus.Cancelled)
        {
            throw new DomainConflictException("Campaign is already cancelled.");
        }

        if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
        {
            throw new DomainConflictException("Completed campaigns cannot be cancelled.");
        }

        Status = CampaignStatus.Cancelled;
        AddDomainEvent(new CampaignCancelledDomainEvent(Id, OwnerId));
    }

    /// <summary>
    /// Transitions a published campaign whose deadline has passed and whose goal was reached to
    /// <see cref="CampaignStatus.Successful"/>. Called by <c>CampaignExpirationBackgroundService</c>;
    /// never opens a transaction against Contributions' database — the campaign only emits
    /// <see cref="CampaignSucceededDomainEvent"/> via its own outbox.
    /// </summary>
    public void CompleteSuccessfully(DateTime nowUtc)
    {
        if (Status != CampaignStatus.Published)
        {
            throw new DomainConflictException($"Only published campaigns can be completed. Current status: '{Status}'.");
        }

        if (RaisedAmount.Amount < GoalAmount.Amount)
        {
            throw new DomainConflictException("Cannot complete a campaign successfully — its funding goal was not reached.");
        }

        if (nowUtc < DeadlineUtc)
        {
            throw new DomainConflictException(
                $"Cannot complete campaign '{Id}' before its deadline '{DeadlineUtc:O}'. Current time: '{nowUtc:O}'.");
        }

        Status = CampaignStatus.Successful;
        AddDomainEvent(new CampaignSucceededDomainEvent(Id, OwnerId, RaisedAmount.Amount, RaisedAmount.Currency, nowUtc));
    }

    /// <summary>
    /// Transitions a published campaign whose deadline has passed without reaching its goal to
    /// <see cref="CampaignStatus.Failed"/>. Emits <see cref="CampaignFailedDomainEvent"/> so
    /// Contributions can asynchronously refund every backer — see
    /// <c>CampaignTerminationRefundHandler</c> — rather than this aggregate reaching into
    /// Contributions' database directly.
    /// </summary>
    public void MarkFailed(DateTime nowUtc)
    {
        if (Status != CampaignStatus.Published)
        {
            throw new DomainConflictException($"Only published campaigns can be marked failed. Current status: '{Status}'.");
        }

        if (RaisedAmount.Amount >= GoalAmount.Amount)
        {
            throw new DomainConflictException("Cannot mark a campaign failed — its funding goal was reached.");
        }

        if (nowUtc < DeadlineUtc)
        {
            throw new DomainConflictException(
                $"Cannot mark campaign '{Id}' failed before its deadline '{DeadlineUtc:O}'. Current time: '{nowUtc:O}'.");
        }

        Status = CampaignStatus.Failed;
        AddDomainEvent(new CampaignFailedDomainEvent(Id, OwnerId, RaisedAmount.Amount, GoalAmount.Amount, RaisedAmount.Currency, nowUtc));
    }

    /// <summary>
    /// Gets a value indicating whether confirmed contributions can be credited: only while the
    /// campaign is published. The deadline is enforced earlier, when the pledge is made, so a
    /// payment the gateway confirms shortly after the deadline is still credited until the
    /// expiration job completes the campaign.
    /// </summary>
    public bool IsAcceptingContributions => Status == CampaignStatus.Published;

    /// <summary>
    /// Creates the ledger entry that credits a confirmed contribution to this campaign. The
    /// campaign's <see cref="RaisedAmount"/> does not change here: the database recomputes it from
    /// the ledger when the entry is appended.
    /// </summary>
    /// <param name="contributionId">The confirmed contribution; the ledger credits it at most once.</param>
    /// <param name="amount">The contribution amount, in the campaign's currency.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The pledge entry to append.</returns>
    /// <exception cref="DomainConflictException">The campaign is not accepting contributions.</exception>
    /// <exception cref="BusinessRuleViolationException">The amount is not positive or the currency differs.</exception>
    public CampaignLedgerEntry RecordPledge(Guid contributionId, Money amount, DateTime nowUtc)
    {
        if (!IsAcceptingContributions)
        {
            throw new DomainConflictException(
                $"Cannot credit contributions to a campaign in status '{Status}'. Only Published campaigns accept funds.");
        }

        EnsureLedgerAmount(amount);
        return CampaignLedgerEntry.Pledge(Id, contributionId, amount, nowUtc);
    }

    /// <summary>
    /// Creates the compensating ledger entry that reverses a refunded contribution. Allowed in any
    /// status: refunds happen precisely because a campaign was cancelled or failed.
    /// </summary>
    /// <param name="contributionId">The refunded contribution; the ledger reverses it at most once, and only if it was credited.</param>
    /// <param name="amount">The refunded amount (positive), in the campaign's currency.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The refund entry (negative amount) to append.</returns>
    /// <exception cref="BusinessRuleViolationException">The amount is not positive or the currency differs.</exception>
    public CampaignLedgerEntry RecordRefund(Guid contributionId, Money amount, DateTime nowUtc)
    {
        EnsureLedgerAmount(amount);
        return CampaignLedgerEntry.Refund(Id, contributionId, amount, nowUtc);
    }

    /// <summary>
    /// Reconstitutes the raised amount from a ledger total, for an aggregate built outside the
    /// database (unit tests, projections). Application code never needs it: the ledger recomputes
    /// the snapshot in SQL and the persistence layer refreshes a loaded aggregate from it. Calling
    /// it cannot bypass the ledger either, because EF Core never writes the raised-amount columns
    /// (their after-save behavior is Ignore).
    /// </summary>
    /// <param name="ledgerTotal">The sum of the campaign's ledger entries.</param>
    public void ApplyLedgerTotal(Money ledgerTotal)
    {
        ArgumentNullException.ThrowIfNull(ledgerTotal);
        EnsureCampaignCurrency(ledgerTotal);
        RaisedAmount = ledgerTotal;
    }

    private void EnsureLedgerAmount(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Amount <= 0)
        {
            throw new BusinessRuleViolationException("Contribution amount must be greater than zero.");
        }

        EnsureCampaignCurrency(amount);
    }

    private void EnsureCampaignCurrency(Money amount)
    {
        if (!string.Equals(amount.Currency, GoalAmount.Currency, StringComparison.Ordinal))
        {
            throw new BusinessRuleViolationException(
                $"Amount currency '{amount.Currency}' does not match campaign currency '{GoalAmount.Currency}'.");
        }
    }

    private static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new BusinessRuleViolationException("OwnerId is required.");
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BusinessRuleViolationException("Campaign title is required.");
        }

        if (title.Trim().Length > 200)
        {
            throw new BusinessRuleViolationException("Campaign title cannot exceed 200 characters.");
        }
    }

    private static void ValidateStory(string story)
    {
        if (string.IsNullOrWhiteSpace(story))
        {
            throw new BusinessRuleViolationException("Campaign story is required.");
        }

        if (story.Trim().Length < 20)
        {
            throw new BusinessRuleViolationException("Campaign story must be at least 20 characters.");
        }
    }

    private static void ValidateCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new BusinessRuleViolationException("Campaign category is required.");
        }

        if (category.Trim().Length > 100)
        {
            throw new BusinessRuleViolationException("Campaign category cannot exceed 100 characters.");
        }
    }

    private static void ValidateGoalAmount(Money goalAmount)
    {
        ArgumentNullException.ThrowIfNull(goalAmount);

        if (goalAmount.Amount <= 0)
        {
            throw new BusinessRuleViolationException("Campaign goal amount must be greater than zero.");
        }
    }

    private static void ValidateDeadline(DateTime deadlineUtc, DateTime createdAtUtc)
    {
        if (deadlineUtc <= createdAtUtc)
        {
            throw new BusinessRuleViolationException("Campaign deadline must be in the future.");
        }
    }
}
