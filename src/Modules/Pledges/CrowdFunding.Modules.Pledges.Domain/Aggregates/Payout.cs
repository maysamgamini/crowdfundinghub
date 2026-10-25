namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// The money owed to a campaign's creator once the campaign succeeded: what was really charged, less the
/// platform's fee. There is one per campaign, and the database enforces it with a unique index on the campaign.
/// </summary>
/// <remarks>
/// The payout waits (<see cref="PayoutStatus.Pending"/>) until every pledge of the campaign has settled, because
/// the amount is not known before that: a card can still be declined. Then the amounts are fixed
/// (<see cref="PayoutStatus.Ready"/>) and only afterwards is the provider asked, so a retry after a timeout sends
/// the same amount under the same key.
/// </remarks>
public sealed class Payout
{
    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PayoutStatus Status { get; private set; }

    public decimal? GrossAmount { get; private set; }

    public decimal? FeeAmount { get; private set; }

    public decimal? NetAmount { get; private set; }

    /// <summary>The fee rate used, in basis points, kept so the amounts can be explained later.</summary>
    public int? PlatformFeeBps { get; private set; }

    /// <summary>How many times the provider answered "no". The next attempt's key uses this number plus one.</summary>
    public int Failures { get; private set; }

    public string? LastFailureCode { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public string? ProviderPayoutId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? PaidAtUtc { get; private set; }

    private Payout()
    {
    }

    public static Payout Create(Guid campaignId, Guid ownerId, string currency, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            OwnerId = ownerId,
            Currency = currency,
            Status = PayoutStatus.Pending,
            CreatedAtUtc = nowUtc,
        };

    /// <summary>The idempotency key for one payout attempt; a retry of the same attempt sends the same key.</summary>
    public static string KeyFor(Guid campaignId, int attemptNumber) => $"payout:{campaignId:N}:{attemptNumber}";

    /// <summary>Fixes the amounts from what was charged. The fee is rounded to the cent, away from zero.</summary>
    public void Prepare(decimal grossAmount, int platformFeeBps)
    {
        if (Status != PayoutStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending payout can be prepared. Current status: '{Status}'.");
        }

        var fee = Math.Round(grossAmount * platformFeeBps / 10_000m, 2, MidpointRounding.AwayFromZero);

        GrossAmount = grossAmount;
        FeeAmount = fee;
        NetAmount = grossAmount - fee;
        PlatformFeeBps = platformFeeBps;
        Status = grossAmount > 0 ? PayoutStatus.Ready : PayoutStatus.NothingToPay;
    }

    public void MarkPaid(string providerPayoutId, DateTime nowUtc)
    {
        if (Status != PayoutStatus.Ready)
        {
            throw new InvalidOperationException($"Only a ready payout can be paid. Current status: '{Status}'.");
        }

        Status = PayoutStatus.Paid;
        ProviderPayoutId = providerPayoutId;
        PaidAtUtc = nowUtc;
        NextAttemptAtUtc = null;
        LastFailureCode = null;
    }

    public void RecordFailure(string failureCode, DateTime nextAttemptAtUtc)
    {
        if (Status != PayoutStatus.Ready)
        {
            throw new InvalidOperationException($"Only a ready payout can fail. Current status: '{Status}'.");
        }

        Failures++;
        LastFailureCode = failureCode;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}

public enum PayoutStatus
{
    /// <summary>The campaign succeeded; some of its pledges have not settled, so the amount is not known yet.</summary>
    Pending = 1,

    /// <summary>The amounts are fixed and the provider is to be asked.</summary>
    Ready = 2,

    /// <summary>The provider paid the creator.</summary>
    Paid = 3,

    /// <summary>Every charge failed, so nothing was collected and nothing is owed.</summary>
    NothingToPay = 4,
}
