using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Moderation.Domain.Enums;
using CrowdFunding.Modules.Moderation.Domain.Events;

namespace CrowdFunding.Modules.Moderation.Domain.Aggregates;

public sealed class CampaignReview : BaseEntity
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public CampaignReviewStatus Status { get; private set; }
    public Guid? ModeratorId { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

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

    public static CampaignReview Create(Guid campaignId, DateTime createdAtUtc)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("CampaignId is required.", nameof(campaignId));
        }

        return new CampaignReview(Guid.NewGuid(), campaignId, createdAtUtc);
    }

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
