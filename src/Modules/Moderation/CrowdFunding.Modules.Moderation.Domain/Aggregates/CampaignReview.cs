using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Moderation.Domain.Enums;
using CrowdFunding.Modules.Moderation.Domain.Events;

namespace CrowdFunding.Modules.Moderation.Domain.Aggregates;

/// <summary>
/// Represents the moderation aggregate that tracks review state for a campaign.
/// </summary>
public sealed class CampaignReview : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public CampaignReviewStatus Status { get; private set; }
    public Guid? ModeratorId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>Toxicity/phishing score from an automated media-safety analysis (0.0-1.0), if
    /// one has run. Persisted even when it doesn't clear the auto-approve threshold, so a human
    /// moderator reviewing the queue sees the AI's finding instead of starting from nothing.
    /// See TICKET-032.</summary>
    public decimal? ToxicityScore { get; private set; }

    /// <summary>Adult-content score from the same automated analysis (0.0-1.0).</summary>
    public decimal? AdultContentScore { get; private set; }

    private CampaignReview()
    {
    }

    private CampaignReview(Guid id, Guid campaignId, DateTime createdAtUtc)
    {
        Id = id;
        CampaignId = campaignId;
        CreatedAtUtc = createdAtUtc;
        Status = CampaignReviewStatus.Pending;
    }

    /// <summary>
    /// Creates a new campaign review in Pending status.
    /// </summary>
    /// <param name="campaignId">The unique identifier of the campaign.</param>
    /// <param name="createdAtUtc">The UTC timestamp when the review was created.</param>
    /// <returns>A new <see cref="CampaignReview"/> instance.</returns>
    public static CampaignReview Create(Guid campaignId, DateTime createdAtUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        return new CampaignReview(Guid.NewGuid(), campaignId, createdAtUtc);
    }

    /// <summary>
    /// Approves the campaign review submission.
    /// </summary>
    /// <param name="moderatorId">The unique identifier of the reviewing moderator.</param>
    /// <param name="notes">Optional approval notes.</param>
    /// <param name="reviewedAtUtc">The UTC timestamp when review was approved.</param>
    public void Approve(Guid moderatorId, string? notes, DateTime reviewedAtUtc)
    {
        EnsurePending();
        ValidateModerator(moderatorId);

        ModeratorId = moderatorId;
        Notes = NormalizeNotes(notes);
        ReviewedAtUtc = reviewedAtUtc;
        Status = CampaignReviewStatus.Approved;
        AddDomainEvent(new CampaignReviewApprovedDomainEvent(CampaignId, ModeratorId!.Value, Notes));
    }

    /// <summary>
    /// Rejects the campaign review submission with reviewer feedback.
    /// </summary>
    /// <param name="moderatorId">The unique identifier of the reviewing moderator.</param>
    /// <param name="notes">Rejection explanation and feedback.</param>
    /// <param name="reviewedAtUtc">The UTC timestamp when review was rejected.</param>
    public void Reject(Guid moderatorId, string? notes, DateTime reviewedAtUtc)
    {
        EnsurePending();
        ValidateModerator(moderatorId);

        ModeratorId = moderatorId;
        Notes = NormalizeNotes(notes);
        ReviewedAtUtc = reviewedAtUtc;
        Status = CampaignReviewStatus.Rejected;
        AddDomainEvent(new CampaignReviewRejectedDomainEvent(CampaignId, ModeratorId!.Value, Notes));
    }

    /// <summary>
    /// Records the result of an asynchronous, serverless media-safety analysis (TICKET-032) —
    /// offloaded out of the request thread precisely because it takes 3-15 seconds, arriving here
    /// later via a signed webhook rather than inline during campaign creation. A score clearing
    /// the gateway's own safety threshold (<paramref name="passedSafetyCheck"/>) auto-approves the
    /// review without waiting on a human moderator; otherwise the scores are simply recorded so a
    /// moderator reviewing the (still Pending) queue sees the AI's finding instead of nothing.
    /// A no-op, not an error, if the review already left Pending by the time this arrives — a
    /// human moderator may well act before a 3-15 second analysis completes.
    /// </summary>
    public void RecordAutomatedMediaAnalysis(bool passedSafetyCheck, decimal toxicityScore, decimal adultContentScore, DateTime analyzedAtUtc)
    {
        if (Status != CampaignReviewStatus.Pending)
        {
            return;
        }

        ToxicityScore = toxicityScore;
        AdultContentScore = adultContentScore;

        if (!passedSafetyCheck)
        {
            return;
        }

        // Guid.Empty marks this as an automated, non-human approval — ModeratorId stays nullable
        // specifically to distinguish "no moderator acted" from "a moderator with this id acted";
        // Guid.Empty here would be indistinguishable from an unset moderator to a reader of this
        // aggregate alone, so the Notes text is the durable record of what actually happened.
        ModeratorId = null;
        Notes = $"Auto-approved by automated media safety analysis (toxicity={toxicityScore:0.00}, adultContent={adultContentScore:0.00}).";
        ReviewedAtUtc = analyzedAtUtc;
        Status = CampaignReviewStatus.Approved;
        AddDomainEvent(new CampaignReviewApprovedDomainEvent(CampaignId, Guid.Empty, Notes));
    }

    private void EnsurePending()
    {
        if (Status != CampaignReviewStatus.Pending)
        {
            throw new InvalidOperationException("Only pending campaign reviews can be updated.");
        }
    }

    private static void ValidateModerator(Guid moderatorId)
    {
        if (moderatorId == Guid.Empty)
        {
            throw new ArgumentException("ModeratorId is required.", nameof(moderatorId));
        }
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var normalized = notes.Trim();

        if (normalized.Length > 500)
        {
            throw new ArgumentException("Review notes cannot exceed 500 characters.", nameof(notes));
        }

        return normalized;
    }
}
