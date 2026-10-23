namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when a pledge insert loses a race against another request
/// that carries the same backer and idempotency key. The caller reloads the winning pledge.
/// </summary>
public sealed class IdempotencyKeyAlreadyUsedException : Exception
{
    public IdempotencyKeyAlreadyUsedException(Exception innerException)
        : base("A pledge with this idempotency key already exists.", innerException)
    {
    }
}
