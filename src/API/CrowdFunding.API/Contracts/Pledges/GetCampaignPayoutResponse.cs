namespace CrowdFunding.API.Contracts.Pledges;

/// <summary>A campaign's payout as the creator sees it, with the ledger lines behind the balance.</summary>
public sealed record GetCampaignPayoutResponse(
    Guid PayoutId,
    Guid CampaignId,
    string Status,
    string Currency,
    decimal? GrossAmount,
    decimal? FeeAmount,
    decimal? NetAmount,
    DateTime? PaidAtUtc,
    decimal Balance,
    IReadOnlyList<PayoutLedgerLineResponse> Lines);

public sealed record PayoutLedgerLineResponse(string Kind, decimal Amount, Guid? PledgeId, DateTime CreatedAtUtc);
