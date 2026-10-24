namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

/// <summary>How a pledge turns into money. The two models trade certainty for cost; see the funding model entry in the design-versus-code table.</summary>
public enum FundingModel
{
    /// <summary>Charge when the backer pledges and refund if the campaign fails. Simple, but every failed campaign costs a refund per backer.</summary>
    ChargeNow = 1,

    /// <summary>Save the card when the backer pledges, charge it once at the deadline if the campaign is funded.</summary>
    SaveThenCharge = 2,
}

public interface IPaymentSettings
{
    FundingModel FundingModel { get; }

    /// <summary>How many numbered charge attempts a pledge gets before it fails for good.</summary>
    int MaxChargeAttempts { get; }

    /// <summary>The wait before the next charge attempt after a decline.</summary>
    TimeSpan ChargeRetryDelay { get; }

    /// <summary>How long a worker holds a charge attempt before another may resume it.</summary>
    TimeSpan ChargeLease { get; }
}
