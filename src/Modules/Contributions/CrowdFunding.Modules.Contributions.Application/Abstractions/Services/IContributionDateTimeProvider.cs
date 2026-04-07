namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Services;

public interface IContributionDateTimeProvider
{
    DateTime UtcNow { get; }
}
