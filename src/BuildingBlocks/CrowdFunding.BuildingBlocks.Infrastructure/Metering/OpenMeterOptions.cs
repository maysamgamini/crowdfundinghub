namespace CrowdFunding.BuildingBlocks.Infrastructure.Metering;

/// <summary>
/// Binds to the "OpenMeter" configuration section — transport config only (fee policy lives in
/// the Application-layer OpenMeterFeeOptions). <see cref="ApiKey"/> is intentionally left unset
/// by default; see appsettings.json for the placeholder and where to supply a real key
/// (user secrets / environment variable / key vault in production, never committed).
/// </summary>
public sealed class OpenMeterOptions
{
    public const string SectionName = "OpenMeter";

    public Uri BaseUrl { get; set; } = new("https://openmeter.cloud");
    public string? ApiKey { get; set; }
}
