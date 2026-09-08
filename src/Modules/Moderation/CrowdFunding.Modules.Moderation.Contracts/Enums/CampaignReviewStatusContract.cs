namespace CrowdFunding.Modules.Moderation.Contracts.Enums;

/// <summary>
/// Cross-module representation of a campaign review's status, mirroring the Moderation
/// Domain's <c>CampaignReviewStatus</c> enum without exposing the Domain type itself across
/// module boundaries.
/// </summary>
public enum CampaignReviewStatusContract
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}
