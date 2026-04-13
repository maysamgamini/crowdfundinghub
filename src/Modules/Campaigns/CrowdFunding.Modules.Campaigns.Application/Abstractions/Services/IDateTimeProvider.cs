namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

/// <summary>
/// Provides the current UTC time to the campaigns module.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
