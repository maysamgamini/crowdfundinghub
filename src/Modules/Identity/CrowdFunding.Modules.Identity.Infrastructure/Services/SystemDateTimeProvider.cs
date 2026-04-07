using CrowdFunding.Modules.Identity.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IIdentityDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
