using CrowdFunding.BuildingBlocks.Domain.Exceptions;

namespace CrowdFunding.Modules.Identity.Domain.Entities;

/// <summary>
/// Represents a role assigned to a user.
/// </summary>
public sealed class UserRoleAssignment
{
    public Guid Id { get; private set; }
    public string Role { get; private set; } = string.Empty;

    private UserRoleAssignment()
    {
    }

    public UserRoleAssignment(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new BusinessRuleViolationException("Role is required.");
        }

        Id = Guid.NewGuid();
        Role = role.Trim();
    }
}
