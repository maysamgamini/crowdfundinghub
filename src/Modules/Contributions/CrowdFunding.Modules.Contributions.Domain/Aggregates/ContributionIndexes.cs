namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

/// <summary>Names of database indexes the application code needs to recognise by name.</summary>
public static class ContributionIndexes
{
    /// <summary>Unique per (backer, idempotency key) where a key was sent.</summary>
    public const string IdempotencyKey = "UX_contributions_ContributorId_IdempotencyKey";
}
