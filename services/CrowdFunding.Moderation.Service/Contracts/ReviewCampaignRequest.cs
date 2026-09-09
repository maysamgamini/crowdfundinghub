namespace CrowdFunding.Moderation.Service.Contracts;

/// <summary>
/// Own, minimal request contract — deliberately not shared with the monolith's
/// <c>CrowdFunding.API.Contracts.Moderation</c> namespace, since referencing the API project
/// would defeat the point of this extraction proof-of-concept.
/// </summary>
public sealed record ReviewCampaignRequest(string? Notes);
