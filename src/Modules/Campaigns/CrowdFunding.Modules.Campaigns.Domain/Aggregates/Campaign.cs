
/*
What is a valid campaign?
When can a campaign be published?
When can a campaign accept contributions?
What state transitions are allowed?
What makes money valid?
*/
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Domain.Enums;

namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

/// <summary>
/// Represents a crowdfunding campaign aggregate root.
/// </summary>
public sealed class Campaign
{
    /// <summary>
    /// Unique identifier for the campaign.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The user who owns/created the campaign.
    /// </summary>
    public Guid OwnerId { get; private set; }

    /// <summary>
    /// Title of the campaign.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Story/description of the campaign.
    /// </summary>
    public string Story { get; private set; } = string.Empty;

    /// <summary>
    /// Category of the campaign (e.g., Health, Education).
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Deadline for the campaign to accept contributions (UTC).
    /// </summary>
    public DateTime DeadlineUtc { get; private set; }

    /// <summary>
    /// Current status of the campaign (Draft, Published, etc.).
    /// </summary>
    public CampaignStatus Status { get; private set; }

    /// <summary>
    /// The goal amount to be raised.
    /// </summary>
    public Money GoalAmount { get; private set; } = null!;

    /// <summary>
    /// The amount raised so far.
    /// </summary>
    public Money RaisedAmount { get; private set; } = null!;

    /// <summary>
    /// When the campaign was created (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Private constructor for ORM/serialization.
    /// </summary>
    private Campaign() { }

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

        return new Campaign(
            Guid.NewGuid(),
            ownerId,
            title.Trim(),
            story.Trim(),
            category.Trim(),
            goalAmount,
            deadlineUtc,
            createdAtUtc);
    }

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
    }

    public void Cancel()
    {
        if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
        {
            throw new InvalidOperationException("Completed campaigns cannot be cancelled.");
        }

        Status = CampaignStatus.Cancelled;
    }

    public void AddContribution(Money contribution)
    {
        if (Status != CampaignStatus.Published)
        {
            throw new InvalidOperationException("Only published campaigns can receive contributions.");
        }

        if (contribution is null)
        {
            throw new ArgumentNullException(nameof(contribution));
        }

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
        if (goalAmount is null)
        {
            throw new ArgumentNullException(nameof(goalAmount));
        }

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
