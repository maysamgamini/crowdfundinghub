namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}