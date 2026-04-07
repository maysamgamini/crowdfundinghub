namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

public sealed record ListCampaignsFilter(
    Guid? OwnerId,
    string? Category,
    string? Status);
