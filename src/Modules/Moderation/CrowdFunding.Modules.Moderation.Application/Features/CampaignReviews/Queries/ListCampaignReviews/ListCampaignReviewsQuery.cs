using CrowdFunding.BuildingBlocks.Application.Pagination;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.ListCampaignReviews;

/// <summary>
/// Represents the request to execute the List Campaign Reviews query — the moderation queue
/// moderators use to find campaigns awaiting a decision, since there was previously no way to
/// discover a review without already knowing its campaign id (improvement.md-adjacent QA
/// TICKET-013 Issue C).
/// </summary>
public sealed record ListCampaignReviewsQuery(PageRequest PageRequest, string? Status);
