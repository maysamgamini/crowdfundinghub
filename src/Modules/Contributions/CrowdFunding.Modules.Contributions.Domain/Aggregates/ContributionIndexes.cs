namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

/// <summary>Names of database indexes the application code needs to recognise by name.</summary>
public static class ContributionIndexes
{
    /// <summary>Unique per (backer, idempotency key) where a key was sent.</summary>
    public const string IdempotencyKey = "UX_contributions_ContributorId_IdempotencyKey";

    /// <summary>Unique per (campaign, backer) among live pledges (Pending or Succeeded): one live pledge per backer per campaign.</summary>
    public const string OneLivePledge = "UX_contributions_one_live_pledge_per_backer";
}
