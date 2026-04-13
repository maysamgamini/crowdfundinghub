using CrowdFunding.Modules.Identity.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Provides the current UTC time to the surrounding module.
/// </summary>
public sealed class SystemDateTimeProvider : IIdentityDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
