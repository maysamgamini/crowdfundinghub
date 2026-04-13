namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Defines the permission names recognized by the application.
/// </summary>
public static class PermissionConstants
{
    public const string CampaignsCreate = "campaigns.create";
    public const string CampaignsPublish = "campaigns.publish";
    public const string CampaignsCancel = "campaigns.cancel";
    public const string CampaignsContribute = "campaigns.contribute";
    public const string ContributionsPaymentsManage = "contributions.payments.manage";
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
        ContributionsPaymentsManage,
        CampaignsManageAny,
        ModerationReview,
        IdentityRolesAssign,
        IdentityPermissionsGrant
    ];
}
