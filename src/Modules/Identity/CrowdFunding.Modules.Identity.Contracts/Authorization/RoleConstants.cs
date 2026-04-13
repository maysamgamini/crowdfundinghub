namespace CrowdFunding.Modules.Identity.Contracts.Authorization;

/// <summary>
/// Defines the built-in role names recognized by the application.
/// </summary>
public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Creator = "Creator";
    public const string Backer = "Backer";

    public static readonly IReadOnlyCollection<string> All =
    [
        Admin,
        Moderator,
        Creator,
        Backer
    ];
}
