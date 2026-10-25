namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;

/// <summary>
/// Thrown by the transaction executor when the same refund attempt of a pledge is recorded twice (two workers raced
/// on it). The provider saw one key, so the first record stands and the second worker's changes are rolled back.
/// </summary>
public sealed class RefundAttemptAlreadyRecordedException : Exception
{
    public RefundAttemptAlreadyRecordedException(Exception innerException)
        : base("This refund attempt was already recorded.", innerException)
    {
    }
}
