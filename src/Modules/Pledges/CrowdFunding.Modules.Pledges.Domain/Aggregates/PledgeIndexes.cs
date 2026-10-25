namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>Names of database indexes the application code needs to recognise by name.</summary>
public static class PledgeIndexes
{
    /// <summary>Unique per (backer, idempotency key) where a key was sent.</summary>
    public const string IdempotencyKey = "UX_pledges_BackerId_IdempotencyKey";

    /// <summary>Unique per (campaign, backer) among live pledges (Pending or Succeeded): one live pledge per backer per campaign.</summary>
    public const string OneLivePledge = "UX_pledges_one_live_pledge_per_backer";

    /// <summary>Unique per pledge: a pledge is activated at most once.</summary>
    public const string OneActivationPerPledge = "UX_pledge_activations_one_per_pledge";

    /// <summary>Unique per campaign: a campaign is paid out at most once.</summary>
    public const string OnePayoutPerCampaign = "UX_payouts_one_per_campaign";

    /// <summary>Unique per (payout, kind, pledge): a payout ledger line about a pledge is written at most once.</summary>
    public const string OnePayoutLedgerLinePerPledge = "UX_payout_ledger_one_line_per_pledge";

    /// <summary>Unique per (payout, kind) among lines about the payout itself, such as the amount paid out.</summary>
    public const string OnePayoutLedgerLinePerPayout = "UX_payout_ledger_one_line_per_payout";

    /// <summary>Each payout ledger kind has its own sign: charges and returned fees are positive, the rest negative.</summary>
    public const string PayoutLedgerSignMatchesKind = "CK_payout_ledger_sign_matches_kind";

    /// <summary>The amounts of a payout add up: net is gross less fee, and none is negative.</summary>
    public const string PayoutAmountsAddUp = "CK_payouts_amounts_add_up";
}
