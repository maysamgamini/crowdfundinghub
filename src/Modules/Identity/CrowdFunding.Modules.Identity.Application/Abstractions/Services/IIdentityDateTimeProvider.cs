namespace CrowdFunding.Modules.Identity.Application.Abstractions.Services;

/// <summary>
/// Provides the current UTC time to the identity module.
/// </summary>
public interface IIdentityDateTimeProvider
{
    DateTime UtcNow { get; }
}
