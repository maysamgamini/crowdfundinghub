namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Maps built-in roles to the permissions they grant.
/// </summary>
public static class RolePermissionCatalog
{
    /// <summary>
    /// Resolves the default set of fine-grained permissions associated with a given role.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <returns>A read-only collection of permission strings.</returns>
    public static IReadOnlyCollection<string> GetPermissionsForRole(string role)
    {
        return role switch
        {
            RoleConstants.Admin => PermissionConstants.All,
            RoleConstants.Moderator => [PermissionConstants.ModerationReview],
            RoleConstants.Creator =>
            [
                PermissionConstants.CampaignsCreate,
                PermissionConstants.CampaignsPublish,
                PermissionConstants.CampaignsCancel
            ],
            RoleConstants.Backer => [PermissionConstants.CampaignsContribute],
            _ => []
        };
    }
}
