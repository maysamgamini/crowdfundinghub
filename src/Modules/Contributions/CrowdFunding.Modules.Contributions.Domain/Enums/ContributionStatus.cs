namespace CrowdFunding.Modules.Contributions.Domain.Enums;

/// <summary>
/// Defines the payment states that a contribution can move through.
/// </summary>
public enum ContributionStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4,

    // The "save the card now, charge at the deadline" funding model. In that model Succeeded still means
    // "the money was taken", so refunds and the live-pledge rule work unchanged.

    /// <summary>The card is saved and the pledge counts toward the campaign, but nothing has been charged.</summary>
    Active = 5,

    /// <summary>The campaign was funded: this pledge is waiting for its charge.</summary>
    ToCharge = 6,

    /// <summary>A charge attempt is in flight with the payment provider.</summary>
    Charging = 7,

    /// <summary>The campaign did not get funded, or was cancelled: the promise ended and nothing was ever charged.</summary>
    Released = 8
}
