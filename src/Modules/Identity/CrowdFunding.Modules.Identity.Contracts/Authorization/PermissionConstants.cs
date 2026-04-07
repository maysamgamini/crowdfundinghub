namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

public static class PermissionConstants
{
    public const string CampaignsCreate = "campaigns.create";
    public const string CampaignsPublish = "campaigns.publish";
    public const string CampaignsCancel = "campaigns.cancel";
    public const string CampaignsContribute = "campaigns.contribute";
    public const string CampaignsManageAny = "campaigns.manage.any";
    public const string ModerationReview = "moderation.review";
    public const string IdentityRolesAssign = "identity.roles.assign";
    public const string IdentityPermissionsGrant = "identity.permissions.grant";

    public static readonly IReadOnlyCollection<string> All =
    [
        CampaignsCreate,
        CampaignsPublish,
        CampaignsCancel,
        CampaignsContribute,
        CampaignsManageAny,
        ModerationReview,
        IdentityRolesAssign,
        IdentityPermissionsGrant
    ];
}
