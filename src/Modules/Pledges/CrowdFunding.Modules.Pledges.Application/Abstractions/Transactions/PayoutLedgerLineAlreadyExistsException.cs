namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when a payout ledger line that already exists is written again, which the
/// unique indexes refuse. The first line stands, so a repeat is safe to ignore.
/// </summary>
public sealed class PayoutLedgerLineAlreadyExistsException : Exception
{
    public PayoutLedgerLineAlreadyExistsException(Exception innerException)
        : base("The payout ledger already has this line.", innerException)
    {
    }
}
