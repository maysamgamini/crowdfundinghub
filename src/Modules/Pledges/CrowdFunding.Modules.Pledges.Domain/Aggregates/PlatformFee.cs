namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// The platform's fee on one pledge. It is decided here, once, and used both when a pledge is metered and when a
/// payout is fixed, so the two can never differ by a cent. The fee is rounded to the cent, away from zero.
/// </summary>
public static class PlatformFee
{
    public static decimal ForPledge(decimal amount, int platformFeeBps)
        => Math.Round(amount * platformFeeBps / 10_000m, 2, MidpointRounding.AwayFromZero);
}
