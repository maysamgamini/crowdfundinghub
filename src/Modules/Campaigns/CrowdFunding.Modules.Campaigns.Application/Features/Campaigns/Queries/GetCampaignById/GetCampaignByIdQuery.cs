namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
/// <summary>
/// Represents the request to execute the Get Campaign By Id query.
/// </summary>
public sealed record GetCampaignByIdQuery(Guid CampaignId);
