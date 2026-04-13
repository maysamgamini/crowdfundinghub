using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.Modules.Identity.Domain.Aggregates;

/// <summary>
/// Represents an application user together with assigned roles and direct permissions.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
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
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        return new User(
            Guid.NewGuid(),
            email.Trim(),
            normalizedEmail,
            normalizedDisplayName,
            passwordHash,
            createdAtUtc);
    }

    public void AssignRole(string role)
    {
        var normalizedRole = NormalizeValue(role, nameof(role));

        if (Roles.Any(x => string.Equals(x.Role, normalizedRole, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Roles.Add(new UserRoleAssignment(normalizedRole));
    }

    public void GrantPermission(string permission)
    {
        var normalizedPermission = NormalizeValue(permission, nameof(permission));

        if (Permissions.Any(x => string.Equals(x.Permission, normalizedPermission, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Permissions.Add(new UserPermissionGrant(normalizedPermission));
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeEmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var normalized = email.Trim();

        if (!normalized.Contains('@'))
        {
            throw new ArgumentException("Email must be a valid email address.", nameof(email));
        }

        return normalized.ToUpperInvariant();
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        var normalized = displayName.Trim();

        if (normalized.Length > 100)
        {
            throw new ArgumentException("Display name cannot exceed 100 characters.", nameof(displayName));
        }

        return normalized;
    }

    private static string NormalizeValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}
