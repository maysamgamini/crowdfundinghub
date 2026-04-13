namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

/// <summary>
/// Provides the current UTC time to the contributions module.
/// </summary>
public interface IContributionDateTimeProvider
{
    DateTime UtcNow { get; }
}
