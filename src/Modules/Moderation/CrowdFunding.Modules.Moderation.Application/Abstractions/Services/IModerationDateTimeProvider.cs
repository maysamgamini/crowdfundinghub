namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

public interface IModerationDateTimeProvider
{
    DateTime UtcNow { get; }
}
