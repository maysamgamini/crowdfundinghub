using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Contributions.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IContributionDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
