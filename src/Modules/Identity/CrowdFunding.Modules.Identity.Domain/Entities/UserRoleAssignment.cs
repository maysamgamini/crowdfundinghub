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
            throw new ArgumentException("Role is required.", nameof(role));
        }

        Id = Guid.NewGuid();
        Role = role.Trim();
    }
}
