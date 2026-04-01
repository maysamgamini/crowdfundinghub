
/*
What is a valid campaign?
When can a campaign be published?
When can a campaign accept contributions?
What state transitions are allowed?
What makes money valid?
*/
using CrowdFunding.Modules.Campaigns.Domain.Enums;
using CrowdFunding.Modules.Campaigns.Domain.ValueObjects;

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

    /// <summary>
    /// Private constructor for creating a new campaign instance.
    /// </summary>
    /// <param name="id">Campaign identifier.</param>
    /// <param name="ownerId">Owner identifier.</param>
    /// <param name="title">Campaign title.</param>
    /// <param name="story">Campaign story.</param>
    /// <param name="category">Campaign category.</param>
    /// <param name="goalAmount">Goal amount.</param>
    /// <param name="deadlineUtc">Deadline in UTC.</param>
    /// <param name="createdAtUtc">Creation date in UTC.</param>
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
        // Initialize raised amount to zero in the same currency as the goal
        RaisedAmount = Money.Zero(goalAmount.Currency);
        DeadlineUtc = deadlineUtc;
        CreatedAtUtc = createdAtUtc;
        // New campaigns start as Draft
        Status = CampaignStatus.Draft;
    }


    /// <summary>
    /// Factory method to create a new campaign with validation.
    /// </summary>
    /// <param name="ownerId">Owner identifier.</param>
    /// <param name="title">Campaign title.</param>
    /// <param name="story">Campaign story.</param>
    /// <param name="category">Campaign category.</param>
    /// <param name="goalAmount">Goal amount.</param>
    /// <param name="deadlineUtc">Deadline in UTC.</param>
    /// <param name="createdAtUtc">Creation date in UTC.</param>
    /// <returns>A new <see cref="Campaign"/> instance.</returns>
    public static Campaign Create(
        Guid ownerId,
        string title,
        string story,
        string category,
        Money goalAmount,
        DateTime deadlineUtc,
        DateTime createdAtUtc)
    {
        // Validate all input parameters
        ValidateOwner(ownerId);
        ValidateTitle(title);
        ValidateStory(story);
        ValidateCategory(category);
        ValidateGoalAmount(goalAmount);
        ValidateDeadline(deadlineUtc, createdAtUtc);

        // Create and return a new campaign instance
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


    /// <summary>
    /// Publishes the campaign if it is in Draft and deadline is in the future.
    /// </summary>
    /// <param name="currentUtc">The current UTC time.</param>
    /// <exception cref="InvalidOperationException">Thrown if the campaign cannot be published.</exception>
    public void Publish(DateTime currentUtc)
    {
        // Only allow publishing from Draft state
        if (Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException("Only draft campaigns can be published.");
        }

        // Deadline must be in the future
        if (DeadlineUtc <= currentUtc)
        {
            throw new InvalidOperationException("Cannot publish a campaign with a past deadline.");
        }

        Status = CampaignStatus.Published;
    }


    /// <summary>
    /// Cancels the campaign unless it is already completed (Successful or Failed).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the campaign cannot be cancelled.</exception>
    public void Cancel()
    {
        if (Status == CampaignStatus.Successful || Status == CampaignStatus.Failed)
        {
            throw new InvalidOperationException("Completed campaigns cannot be cancelled.");
        }

        Status = CampaignStatus.Cancelled;
    }


    /// <summary>
    /// Adds a contribution to the campaign if it is published and the amount is valid.
    /// </summary>
    /// <param name="contribution">The contribution to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the campaign cannot receive contributions or the amount is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the contribution is null.</exception>
    public void AddContribution(Money contribution)
    {
        // Only published campaigns can receive contributions
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

        // Add the contribution to the raised amount
        RaisedAmount = RaisedAmount.Add(contribution);
    }


    /// <summary>
    /// Validates that the owner ID is not empty.
    /// </summary>
    /// <param name="ownerId">Owner identifier.</param>
    /// <exception cref="ArgumentException">Thrown if the ownerId is empty.</exception>
    private static void ValidateOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("OwnerId is required.", nameof(ownerId));
        }
    }


    /// <summary>
    /// Validates the campaign title (required, max 200 chars).
    /// </summary>
    /// <param name="title">Campaign title.</param>
    /// <exception cref="ArgumentException">Thrown if the title is invalid.</exception>
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


    /// <summary>
    /// Validates the campaign story (required, min 20 chars).
    /// </summary>
    /// <param name="story">Campaign story.</param>
    /// <exception cref="ArgumentException">Thrown if the story is invalid.</exception>
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


    /// <summary>
    /// Validates the campaign category (required, max 100 chars).
    /// </summary>
    /// <param name="category">Campaign category.</param>
    /// <exception cref="ArgumentException">Thrown if the category is invalid.</exception>
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


    /// <summary>
    /// Validates the goal amount (required, &gt; 0).
    /// </summary>
    /// <param name="goalAmount">Goal amount.</param>
    /// <exception cref="ArgumentNullException">Thrown if the goal amount is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the goal amount is invalid.</exception>
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

    /// <summary>
    /// Validates that the deadline is after the creation date.
    /// </summary>
    /// <param name="deadlineUtc">Deadline in UTC.</param>
    /// <param name="createdAtUtc">Creation date in UTC.</param>
    /// <exception cref="ArgumentException">Thrown if the deadline is not in the future.</exception>
    private static void ValidateDeadline(DateTime deadlineUtc, DateTime createdAtUtc)
    {
        if (deadlineUtc <= createdAtUtc)
        {
            throw new ArgumentException("Campaign deadline must be in the future.", nameof(deadlineUtc));
        }
    }
}