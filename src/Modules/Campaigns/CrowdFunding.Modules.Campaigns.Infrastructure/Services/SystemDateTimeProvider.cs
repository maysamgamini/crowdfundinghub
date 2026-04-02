using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}