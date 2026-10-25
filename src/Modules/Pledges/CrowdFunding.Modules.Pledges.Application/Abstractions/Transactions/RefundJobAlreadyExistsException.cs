namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when a second refund job for the same campaign reaches the database, which the
/// unique index on the campaign refuses. The first one stands.
/// </summary>
public sealed class RefundJobAlreadyExistsException : Exception
{
    public RefundJobAlreadyExistsException(Exception innerException)
        : base("The campaign already has a refund job.", innerException)
    {
    }
}
