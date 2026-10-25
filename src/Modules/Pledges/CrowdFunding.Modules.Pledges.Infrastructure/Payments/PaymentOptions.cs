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

    /// <summary>How often the settlement worker looks for pledges that are due to be charged.</summary>
    public TimeSpan SettlementPollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How many due pledges one pass claims.</summary>
    public int SettlementBatchSize { get; init; } = 20;
}
