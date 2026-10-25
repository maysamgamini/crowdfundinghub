namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetCampaignPayout;

/// <summary>
/// A campaign's payout. <see cref="Balance"/> is the sum of the ledger: zero once the creator was paid and nothing was
/// refunded afterwards, negative when a pledge was refunded after it was counted (the creator owes that back).
/// </summary>
public sealed record GetCampaignPayoutResult(
    Guid PayoutId,
    Guid CampaignId,
    string Status,
    string Currency,
    decimal? GrossAmount,
    decimal? FeeAmount,
    decimal? NetAmount,
    DateTime? PaidAtUtc,
    decimal Balance,
    IReadOnlyList<PayoutLedgerLineResult> Lines);

public sealed record PayoutLedgerLineResult(string Kind, decimal Amount, Guid? PledgeId, DateTime CreatedAtUtc);
