using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

/// <summary>
/// Provides the current UTC time to the surrounding module.
/// </summary>
public sealed class SystemDateTimeProvider : IModerationDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
