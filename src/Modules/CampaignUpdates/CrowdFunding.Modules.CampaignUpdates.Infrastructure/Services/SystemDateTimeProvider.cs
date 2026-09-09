using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Services;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Services;

public sealed class SystemDateTimeProvider : ICampaignUpdatesDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
