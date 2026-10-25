namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// One line of a payout's ledger. The ledger is append-only: nothing is corrected, a mistake is undone by a new
/// entry. Amounts are signed from the creator's side: money owed to the creator is positive, money taken from them is
/// negative, so the sum of a payout's entries is what is still owed to the creator (never positive once paid) or what
/// the creator owes back (negative).
/// </summary>
public sealed class PayoutLedgerEntry
{
    public Guid Id { get; private set; }

    public Guid PayoutId { get; private set; }

    public PayoutLedgerKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    /// <summary>The pledge the line is about; empty for the line that records the payout itself.</summary>
    public Guid? PledgeId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private PayoutLedgerEntry()
    {
    }

    public static PayoutLedgerEntry Charge(Guid payoutId, Guid pledgeId, decimal amount, string currency, DateTime nowUtc)
        => Create(payoutId, PayoutLedgerKind.Charge, amount, currency, pledgeId, nowUtc);

    public static PayoutLedgerEntry PlatformFee(Guid payoutId, Guid pledgeId, decimal fee, string currency, DateTime nowUtc)
        => Create(payoutId, PayoutLedgerKind.PlatformFee, -fee, currency, pledgeId, nowUtc);

    public static PayoutLedgerEntry PaidOut(Guid payoutId, decimal net, string currency, DateTime nowUtc)
        => Create(payoutId, PayoutLedgerKind.PaidOut, -net, currency, null, nowUtc);

    public static PayoutLedgerEntry ChargeRefunded(Guid payoutId, Guid pledgeId, decimal amount, string currency, DateTime nowUtc)
        => Create(payoutId, PayoutLedgerKind.ChargeRefunded, -amount, currency, pledgeId, nowUtc);

    public static PayoutLedgerEntry FeeReturned(Guid payoutId, Guid pledgeId, decimal fee, string currency, DateTime nowUtc)
        => Create(payoutId, PayoutLedgerKind.FeeReturned, fee, currency, pledgeId, nowUtc);

    private static PayoutLedgerEntry Create(Guid payoutId, PayoutLedgerKind kind, decimal amount, string currency, Guid? pledgeId, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            PayoutId = payoutId,
            Kind = kind,
            Amount = amount,
            Currency = currency,
            PledgeId = pledgeId,
            CreatedAtUtc = nowUtc,
        };
}

public enum PayoutLedgerKind
{
    /// <summary>A pledge that was really charged and counts toward the payout. Positive.</summary>
    Charge = 1,

    /// <summary>The platform's fee on that pledge. Negative.</summary>
    PlatformFee = 2,

    /// <summary>The net amount sent to the creator. Negative.</summary>
    PaidOut = 3,

    /// <summary>A pledge counted in the payout was refunded afterwards: the creator owes it back. Negative.</summary>
    ChargeRefunded = 4,

    /// <summary>The platform gives back its fee on that refunded pledge. Positive.</summary>
    FeeReturned = 5,
}
