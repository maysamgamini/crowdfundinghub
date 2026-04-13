namespace CrowdFunding.Modules.Contributions.Domain.Enums;

/// <summary>
/// Defines the payment states that a contribution can move through.
/// </summary>
public enum ContributionStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3
}
