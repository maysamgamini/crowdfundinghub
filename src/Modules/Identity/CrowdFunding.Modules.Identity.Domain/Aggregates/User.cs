using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.Exceptions;
using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.Modules.Identity.Domain.Aggregates;

/// <summary>
/// Represents an application user together with assigned roles and direct permissions.
/// </summary>
public sealed class User : BaseEntity
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid SecurityStamp { get; private set; } = Guid.NewGuid();
    public List<UserRoleAssignment> Roles { get; private set; } = [];
    public List<UserPermissionGrant> Permissions { get; private set; } = [];

    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string normalizedEmail,
        string displayName,
        string passwordHash,
        DateTime createdAtUtc)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public static User Register(
        string email,
        string displayName,
        string passwordHash,
        DateTime createdAtUtc)
    {
        var normalizedEmail = NormalizeEmailAddress(email);
        var normalizedDisplayName = NormalizeDisplayName(displayName);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new BusinessRuleViolationException("Password hash is required.");
        }

        return new User(
            Guid.NewGuid(),
            email.Trim(),
            normalizedEmail,
            normalizedDisplayName,
            passwordHash,
            createdAtUtc);
    }

    /// <summary>
    /// Assigns a role to the user if not already present.
    /// </summary>
    /// <param name="role">The role name to assign.</param>
    public void AssignRole(string role)
    {
        var normalizedRole = NormalizeValue(role, nameof(role));

        if (Roles.Any(x => string.Equals(x.Role, normalizedRole, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Roles.Add(new UserRoleAssignment(normalizedRole));
    }

    /// <summary>
    /// Grants an explicit permission to the user if not already present.
    /// </summary>
    /// <param name="permission">The permission string to grant.</param>
    public void GrantPermission(string permission)
    {
        var normalizedPermission = NormalizeValue(permission, nameof(permission));

        if (Permissions.Any(x => string.Equals(x.Permission, normalizedPermission, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Permissions.Add(new UserPermissionGrant(normalizedPermission));
    }

    /// <summary>
    /// Deactivates the user account and rotates the security stamp, so that any access tokens
    /// already issued to this user (which embed the old stamp) can be recognized as stale by
    /// consumers even though the JWT signature itself remains valid until natural expiration.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        RotateSecurityStamp();
    }

    /// <summary>
    /// Rotates the security stamp, invalidating every access token issued before this call (they
    /// carry the previous stamp value as a claim). Callers that need instant, sub-token-lifetime
    /// revocation (see TICKET-036) must read <see cref="SecurityStamp"/> before calling this and
    /// publish the old value to the distributed revocation blacklist themselves — the domain
    /// model has no knowledge of Redis or any other infrastructure concern.
    /// </summary>
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Trims and converts an email address to lowercase for normalized lookups.
    /// </summary>
    /// <param name="email">The email address to normalize.</param>
    /// <returns>The normalized lowercase email address.</returns>
    public static string NormalizeEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleViolationException("Email is required.");
        }

        var normalized = email.Trim();

        if (!normalized.Contains('@'))
        {
            throw new BusinessRuleViolationException("Email must be a valid email address.");
        }

        return normalized.ToUpperInvariant();
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleViolationException("Display name is required.");
        }

        var normalized = displayName.Trim();

        if (normalized.Length > 100)
        {
            throw new BusinessRuleViolationException("Display name cannot exceed 100 characters.");
        }

        return normalized;
    }

    private static string NormalizeValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleViolationException($"{parameterName} is required.");
        }

        return value.Trim();
    }
}
