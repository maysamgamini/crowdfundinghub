using CrowdFunding.BuildingBlocks.Domain.Exceptions;

namespace CrowdFunding.Modules.Identity.Domain.Entities;

/// <summary>
/// Represents a direct permission granted to a user.
/// </summary>
public sealed class UserPermissionGrant
{
    public Guid Id { get; private set; }
    public string Permission { get; private set; } = string.Empty;

    private UserPermissionGrant()
    {
    }

    public UserPermissionGrant(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new BusinessRuleViolationException("Permission is required.");
        }

        Id = Guid.NewGuid();
        Permission = permission.Trim();
    }
}
