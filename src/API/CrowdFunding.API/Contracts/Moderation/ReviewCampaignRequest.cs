namespace CrowdFunding.API.Contracts.Moderation;

public sealed record ReviewCampaignRequest(Guid ModeratorId, string? Notes);
