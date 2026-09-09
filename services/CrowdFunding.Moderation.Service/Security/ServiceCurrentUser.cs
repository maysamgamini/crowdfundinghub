using System.Security.Claims;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Moderation.Service.Security;

/// <summary>
/// Adapts the ASP.NET Core HTTP context into <see cref="ICurrentUser"/> — a small duplicate of
/// the monolith's <c>CrowdFunding.API.Security.HttpContextCurrentUser</c>. Moderation's own
/// <c>ApproveCampaignReviewCommandHandler</c>/<c>RejectCampaignReviewCommandHandler</c> already
/// depend on <see cref="ICurrentUser"/> to enforce the "moderation.review" permission claim;
/// this class exists only to satisfy that same abstraction from a host that cannot reference
/// <c>CrowdFunding.API</c>. Identity.Contracts (for <see cref="CustomClaimTypes"/>) is already a
/// transitive dependency of Moderation.Application, not a new coupling introduced here.
/// </summary>
public sealed class ServiceCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServiceCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }
    }

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Roles => GetClaimValues(ClaimTypes.Role);

    public IReadOnlyCollection<string> Permissions => GetClaimValues(CustomClaimTypes.Permission);

    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private string[] GetClaimValues(string claimType)
    {
        return _httpContextAccessor.HttpContext?.User?.Claims
            .Where(claim => claim.Type == claimType)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }
}
