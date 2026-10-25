namespace CrowdFunding.Modules.Pledges.Domain.Aggregates;

/// <summary>
/// The record that a pledge's card was saved. There is at most one per pledge, and the database enforces it:
/// the status check in <see cref="Pledge.ActivatePledge"/> is the fast, readable rule, and the unique
/// index on the pledge is the last word when two requests get past that check at the same moment.
/// </summary>
public sealed class PledgeActivation
{
    public Guid Id { get; private set; }

    public Guid PledgeId { get; private set; }

    public string PaymentMethodReference { get; private set; } = string.Empty;

    public DateTime ActivatedAtUtc { get; private set; }

    private PledgeActivation()
    {
    }

    public PledgeActivation(Guid pledgeId, string paymentMethodReference, DateTime activatedAtUtc)
    {
        Id = Guid.NewGuid();
        PledgeId = pledgeId;
        PaymentMethodReference = paymentMethodReference;
        ActivatedAtUtc = activatedAtUtc;
    }
}
