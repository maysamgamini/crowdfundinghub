namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when a pledge insert loses a race against another live pledge by the
/// same backer on the same campaign, which the one-live-pledge index refuses.
/// </summary>
public sealed class AlreadyBackingException : Exception
{
    public AlreadyBackingException(Exception innerException)
        : base("The backer already has a live pledge on this campaign.", innerException)
    {
    }
}
