namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;

public interface ICampaignUpdatesDateTimeProvider
{
    DateTime UtcNow { get; }
}
