namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Maps built-in roles to the permissions they grant.
/// </summary>
public static class RolePermissionCatalog
{
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
