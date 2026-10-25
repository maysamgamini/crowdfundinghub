using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Payments;

/// <summary>Bound from the <c>Payments</c> configuration section.</summary>
public sealed class PaymentOptions : IPaymentSettings
{
    public const string SectionName = "Payments";

    public FundingModel FundingModel { get; init; } = FundingModel.ChargeNow;

    public int MaxChargeAttempts { get; init; } = 3;

    public TimeSpan ChargeRetryDelay { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan ChargeLease { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>The wait before a payout the provider refused is tried again.</summary>
    public TimeSpan PayoutRetryDelay { get; init; } = TimeSpan.FromHours(1);

    /// <summary>How many refunds one pass of the refund job may start. With a 30 second pass this is the refunds per half minute.</summary>
    public int RefundsPerPass { get; init; } = 50;

    /// <summary>The wait before a refund the provider refused is tried again.</summary>
    public TimeSpan RefundRetryDelay { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How many refund attempts a pledge gets before the refund job leaves it for a person.</summary>
    public int MaxRefundAttempts { get; init; } = 5;

    /// <summary>How often the settlement worker looks for pledges that are due to be charged.</summary>
    public TimeSpan SettlementPollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How many due pledges one pass claims.</summary>
    public int SettlementBatchSize { get; init; } = 20;
}
