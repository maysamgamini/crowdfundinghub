using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users;

internal static class UserAuthorizationProjection
{
    public static string[] GetRoles(User user)
    {
        return user.Roles
            .Select(x => x.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string[] GetExplicitPermissions(User user)
    {
        return user.Permissions
            .Select(x => x.Permission)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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
