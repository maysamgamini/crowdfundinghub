using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.UnitTests;

/// <summary>Settings for the unit tests of the funding model; the defaults match the real defaults.</summary>
internal sealed class FakePaymentSettings(FundingModel fundingModel = FundingModel.ChargeNow) : IPaymentSettings
{
    public FundingModel FundingModel { get; } = fundingModel;

    public int MaxChargeAttempts { get; init; } = 3;

    public TimeSpan ChargeRetryDelay { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan ChargeLease { get; init; } = TimeSpan.FromMinutes(2);
}
