namespace CrowdFunding.Modules.Contributions.Domain.Aggregates;

/// <summary>
/// One numbered attempt to charge a pledge's saved card. The provider's idempotency key is built from the
/// pledge and the attempt number: a retry of the same attempt (after a timeout or a crash) sends the same key,
/// and a new attempt gets a new number and a new key only after the previous one is known to have failed.
/// </summary>
public sealed class ChargeAttempt
{
    public Guid ContributionId { get; private set; }

    public int AttemptNumber { get; private set; }

    /// <summary>The idempotency key sent to the provider for this attempt.</summary>
    public string ProviderKey { get; private set; } = string.Empty;

    public ChargeAttemptStatus Status { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public string? DeclineCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private ChargeAttempt()
    {
    }

    public ChargeAttempt(Guid contributionId, int attemptNumber, string providerKey, DateTime createdAtUtc)
    {
        ContributionId = contributionId;
        AttemptNumber = attemptNumber;
        ProviderKey = providerKey;
        Status = ChargeAttemptStatus.InFlight;
        CreatedAtUtc = createdAtUtc;
    }

    public static string KeyFor(Guid contributionId, int attemptNumber) => $"contribution:{contributionId:N}:charge:{attemptNumber}";

    public void Succeed(string providerPaymentId)
    {
        Status = ChargeAttemptStatus.Succeeded;
        ProviderPaymentId = providerPaymentId;
        DeclineCode = null;
    }

    public void Fail(string declineCode)
    {
        Status = ChargeAttemptStatus.Failed;
        DeclineCode = declineCode;
    }
}

public enum ChargeAttemptStatus
{
    InFlight = 1,
    Succeeded = 2,
    Failed = 3,
}
