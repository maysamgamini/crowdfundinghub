namespace CrowdFunding.Modules.Campaigns.Contracts.Enums;

/// <summary>
/// Cross-module representation of a campaign's lifecycle status, mirroring the Campaigns
/// Domain's <c>CampaignStatus</c> enum without exposing the Domain type itself across module
/// boundaries.
/// </summary>
public enum CampaignStatusContract
{
    Draft = 1,
    Published = 2,
    Successful = 3,
    Failed = 4,
    Cancelled = 5,
}
