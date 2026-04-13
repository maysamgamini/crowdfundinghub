namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

/// <summary>
/// Provides the current UTC time to the moderation module.
/// </summary>
public interface IModerationDateTimeProvider
{
    DateTime UtcNow { get; }
}
