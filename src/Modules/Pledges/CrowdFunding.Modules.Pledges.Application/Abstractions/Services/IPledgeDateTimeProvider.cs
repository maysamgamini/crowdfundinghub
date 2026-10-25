namespace CrowdFunding.Modules.Pledges.Application.Abstractions.Services;

/// <summary>
/// Provides the current UTC time to the pledges module.
/// </summary>
public interface IPledgeDateTimeProvider
{
    DateTime UtcNow { get; }
}
