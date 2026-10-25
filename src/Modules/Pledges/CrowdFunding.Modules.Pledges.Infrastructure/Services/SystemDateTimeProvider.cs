using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Pledges.Infrastructure.Services;

/// <summary>
/// Provides the current UTC time to the surrounding module.
/// </summary>
public sealed class SystemDateTimeProvider : IPledgeDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
