namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;

/// <summary>
/// Represents the request to execute the Get Pledge By Id query — the single-resource read
/// a client needs after `POST .../pledges` to poll payment status, previously missing
/// entirely (QA TICKET-006).
/// </summary>
public sealed record GetPledgeByIdQuery(Guid CampaignId, Guid PledgeId);
