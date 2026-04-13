namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

/// <summary>
/// Represents filter criteria for List Campaigns.
/// </summary>
public sealed record ListCampaignsFilter(
    Guid? OwnerId,
    string? Category,
    string? Status);
