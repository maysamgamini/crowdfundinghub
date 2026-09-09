using CrowdFunding.BuildingBlocks.Application.Audit;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

/// <summary>
/// Represents the request to execute the Cancel Campaign use case.
/// </summary>
// TICKET-039: always forensically audited, regardless of who cancels — not just when the caller
// happens to be an Admin (a campaign owner can also cancel their own campaign; see
// CancelCampaignCommandHandler's ownership check).
[AuditableAction("Campaign.Cancel")]
public sealed record CancelCampaignCommand(Guid CampaignId);
