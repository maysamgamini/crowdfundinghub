namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;

/// <summary>
/// Represents the outcome returned by List Pledges By Campaign.
/// </summary>
public sealed record ListPledgesByCampaignResult(
    Guid Id,
    Guid CampaignId,
    Guid BackerId,
    decimal Amount,
    string Currency,
    string Status,
    string? PaymentReference,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
