using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries;

/// <summary>
/// Reading reviews is a moderator action, like approving or rejecting them. The endpoint attribute in
/// the monolith checks the same permission, but an extracted host does not carry the monolith's policy
/// setup, so the rule is enforced here where every host runs it.
/// </summary>
internal static class ModerationReadAccess
{
    public static void EnsureCanRead(ICurrentUser currentUser)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to read campaign reviews.");
        }

        if (!currentUser.HasPermission(PermissionConstants.ModerationReview))
        {
            throw new ForbiddenAccessException("The current user does not have permission to read campaign reviews.");
        }
    }
}
