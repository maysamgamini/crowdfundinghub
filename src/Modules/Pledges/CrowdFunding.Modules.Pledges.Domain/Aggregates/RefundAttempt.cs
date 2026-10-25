namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// One answered refund attempt of one pledge: the idempotency key it was sent under and what the provider said. An
/// attempt that got no answer (a timeout) leaves no row, so the next try is the same attempt with the same key.
/// </summary>
public sealed class RefundAttempt
{
    public Guid Id { get; private set; }

    public Guid PledgeId { get; private set; }

    public int AttemptNumber { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public RefundAttemptOutcome Outcome { get; private set; }

    public string? FailureCode { get; private set; }

    public string? ProviderRefundId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private RefundAttempt()
    {
    }

    public static RefundAttempt Succeeded(Guid pledgeId, int attemptNumber, string idempotencyKey, string? providerRefundId, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            PledgeId = pledgeId,
            AttemptNumber = attemptNumber,
            IdempotencyKey = idempotencyKey,
            Outcome = RefundAttemptOutcome.Succeeded,
            ProviderRefundId = providerRefundId,
            CreatedAtUtc = nowUtc,
        };

    public static RefundAttempt Failed(Guid pledgeId, int attemptNumber, string idempotencyKey, string failureCode, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            PledgeId = pledgeId,
            AttemptNumber = attemptNumber,
            IdempotencyKey = idempotencyKey,
            Outcome = RefundAttemptOutcome.Failed,
            FailureCode = failureCode,
            CreatedAtUtc = nowUtc,
        };
}

public enum RefundAttemptOutcome
{
    Succeeded = 1,
    Failed = 2,
}
