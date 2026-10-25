namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Defines the permission names recognized by the application.
/// </summary>
public static class PermissionConstants
{
    public const string CampaignsCreate = "campaigns.create";
    public const string CampaignsPublish = "campaigns.publish";
    public const string CampaignsCancel = "campaigns.cancel";
    public const string CampaignsPledge = "campaigns.pledge";
    public const string PledgesPaymentsManage = "pledges.payments.manage";
    public const string CampaignsManageAny = "campaigns.manage.any";
    public const string ModerationReview = "moderation.review";
    public const string IdentityRolesAssign = "identity.roles.assign";
    public const string IdentityPermissionsGrant = "identity.permissions.grant";
    public const string UsersManage = "users.manage";

    public static readonly IReadOnlyCollection<string> All =
    [
        CampaignsCreate,
        CampaignsPublish,
        CampaignsCancel,
        CampaignsPledge,
        PledgesPaymentsManage,
        CampaignsManageAny,
        ModerationReview,
        IdentityRolesAssign,
        IdentityPermissionsGrant,
        UsersManage
    ];
}
