using CrowdFunding.BuildingBlocks.Domain.Common;
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
            throw new InvalidOperationException("Only draft campaigns can be published.");
        }

        if (DeadlineUtc <= currentUtc)
        {
            throw new InvalidOperationException("Cannot publish a campaign with a past deadline.");
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
            throw new InvalidOperationException("Campaign is already cancelled.");
        }

        if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
        {
            throw new InvalidOperationException("Completed campaigns cannot be cancelled.");
        }

        Status = CampaignStatus.Cancelled;
        AddDomainEvent(new CampaignCancelledDomainEvent(Id, OwnerId));
    }

    /// <summary>
    /// Applies a confirmed monetary contribution to the raised balance.
    /// </summary>
    /// <param name="contribution">The monetary contribution to apply.</param>
    public void ApplyConfirmedContribution(Money contribution)
    {
        if (Status != CampaignStatus.Published)
        {
            throw new InvalidOperationException(
                $"Cannot apply contributions to campaign in status '{Status}'. Only Published campaigns can accept funds.");
        }

        ArgumentNullException.ThrowIfNull(contribution);

        if (contribution.Amount <= 0)
        {
            throw new InvalidOperationException("Contribution amount must be greater than zero.");
        }

        RaisedAmount = RaisedAmount.Add(contribution);
    }

    private static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Campaign title is required.", nameof(title));
        }

        if (title.Trim().Length > 200)
        {
            throw new ArgumentException("Campaign title cannot exceed 200 characters.", nameof(title));
        }
    }

    private static void ValidateStory(string story)
    {
        if (string.IsNullOrWhiteSpace(story))
        {
            throw new ArgumentException("Campaign story is required.", nameof(story));
        }

        if (story.Trim().Length < 20)
        {
            throw new ArgumentException("Campaign story must be at least 20 characters.", nameof(story));
        }
    }

    private static void ValidateCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Campaign category is required.", nameof(category));
        }

        if (category.Trim().Length > 100)
        {
            throw new ArgumentException("Campaign category cannot exceed 100 characters.", nameof(category));
        }
    }

    private static void ValidateGoalAmount(Money goalAmount)
    {
        ArgumentNullException.ThrowIfNull(goalAmount);

        if (goalAmount.Amount <= 0)
        {
            throw new ArgumentException("Campaign goal amount must be greater than zero.", nameof(goalAmount));
        }
    }

    private static void ValidateDeadline(DateTime deadlineUtc, DateTime createdAtUtc)
    {
        if (deadlineUtc <= createdAtUtc)
        {
            throw new ArgumentException("Campaign deadline must be in the future.", nameof(deadlineUtc));
        }
    }
}
