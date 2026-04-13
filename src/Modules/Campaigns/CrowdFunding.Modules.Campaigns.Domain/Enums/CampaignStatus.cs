namespace CrowdFunding.Modules.Campaigns.Domain.Enums;

/// <summary>
/// Defines the lifecycle states that a campaign can move through.
/// </summary>
public enum CampaignStatus
{
    Draft = 1,
    Published = 2,
    Successful = 3,
    Failed = 4,
    Cancelled = 5
}
