namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

public interface IIdentityDateTimeProvider
{
    DateTime UtcNow { get; }
}
