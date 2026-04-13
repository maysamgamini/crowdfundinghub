namespace CrowdFunding.BuildingBlocks.Application.Security;

/// <summary>
/// Exposes the current authenticated user to application handlers.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}
