namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when a second activation of the same pledge reaches the database, which the
/// unique index on the pledge refuses. The caller reloads the pledge: it is already active.
/// </summary>
public sealed class PledgeAlreadyActivatedException : Exception
{
    public PledgeAlreadyActivatedException(Exception innerException)
        : base("The pledge was already activated.", innerException)
    {
    }
}
