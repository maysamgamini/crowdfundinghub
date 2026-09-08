using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users;

/// <summary>
/// Represents User Authorization Projection.
/// </summary>
internal static class UserAuthorizationProjection
{
    /// <summary>
    /// Extracts role names assigned to the user.
    /// </summary>
    /// <param name="user">The user aggregate.</param>
    /// <returns>An array of role strings.</returns>
    public static string[] GetRoles(User user)
    {
        return user.Roles
            .Select(x => x.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Extracts explicit permissions directly granted to the user.
    /// </summary>
    /// <param name="user">The user aggregate.</param>
    /// <returns>An array of permission strings.</returns>
    public static string[] GetExplicitPermissions(User user)
    {
        return user.Permissions
            .Select(x => x.Permission)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Computes the union of role-derived and explicit permissions for the user.
    /// </summary>
    /// <param name="user">The user aggregate.</param>
    /// <returns>An array of effective permission strings.</returns>
    public static string[] GetEffectivePermissions(User user)
    {
        return GetRoles(user)
            .SelectMany(RolePermissionCatalog.GetPermissionsForRole)
            .Concat(GetExplicitPermissions(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
