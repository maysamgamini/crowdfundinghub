using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IModerationDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
