namespace CrowdFunding.Modules.Campaigns.Domain.Enums;

/// <summary>
/// What a <see cref="Aggregates.CampaignLedgerEntry"/> records. Stored as an integer; the
/// database CHECK constraint ties each kind to the sign of its amount.
/// </summary>
public enum CampaignLedgerEntryKind
{
    /// <summary>A confirmed contribution credited to the campaign (positive amount).</summary>
    Pledge = 0,

    /// <summary>A compensating entry that reverses a previously credited pledge (negative amount).</summary>
    Refund = 1,

    /// <summary>
    /// The campaign's raised amount carried over when the ledger was introduced for existing data
    /// (one per campaign at most, no contribution id). Never written by application code.
    /// </summary>
    OpeningBalance = 2,
}
