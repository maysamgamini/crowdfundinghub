using CrowdFunding.BuildingBlocks.Application.Security;

namespace CrowdFunding.UnitTests;

internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; init; } = true;
    public Guid UserId { get; init; } = Guid.NewGuid();
    public string? Email { get; init; } = "user@example.com";
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public IReadOnlyCollection<string> Permissions { get; init; } = [];

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsInRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
