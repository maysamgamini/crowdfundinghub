namespace CrowdFunding.Modules.Campaigns.Domain.Enums;

/// <summary>What a campaign ledger entry records. The ledger only ever grows; a refund is a new negative entry, not an edit.</summary>
public enum LedgerEntryKind
{
    /// <summary>A confirmed pledge counted for the campaign. The amount is positive.</summary>
    Pledge = 1,

    /// <summary>The compensating entry for a refunded or refused pledge. The amount is negative.</summary>
    Refund = 2,
}
