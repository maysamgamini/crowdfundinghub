namespace CrowdFunding.BuildingBlocks.Application.Metering;

/// <summary>
/// Fee policy used to project a confirmed pledge's gross amount into platform/processing fees
/// for metering (improvement.md §4.1). Kept in the Application layer, separate from
/// OpenMeterOptions (transport config: base URL, API key), because fee rates are a business
/// policy an application-layer handler needs, not an HTTP client concern.
/// </summary>
public sealed class OpenMeterFeeOptions
{
    public const string SectionName = "Metering:Fees";

    /// <summary>Platform success fee taken from each confirmed pledge, in basis points (500 = 5%).</summary>
    public int PlatformFeeBps { get; set; } = 500;

    /// <summary>Payment processing fee: fixed cents + basis points, modeled on Stripe's $0.30 + 2.9%.</summary>
    public int ProcessingFeeFixedCents { get; set; } = 30;
    public int ProcessingFeeBps { get; set; } = 290;
}
