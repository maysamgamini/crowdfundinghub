using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Services;

/// <summary>
/// Provides the current UTC time to the surrounding module.
/// </summary>
public sealed class SystemDateTimeProvider : IContributionDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
