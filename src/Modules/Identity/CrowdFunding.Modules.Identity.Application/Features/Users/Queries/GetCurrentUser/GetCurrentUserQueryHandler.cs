using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;

/// <summary>
/// Handles Get Current User query requests.
/// </summary>
public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, GetCurrentUserResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
    }

    public async Task<GetCurrentUserResult> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user is not authenticated.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{_currentUser.UserId}' was not found.");

        return new GetCurrentUserResult(
            user.Id,
            user.Email,
            user.DisplayName,
            UserAuthorizationProjection.GetRoles(user),
            UserAuthorizationProjection.GetExplicitPermissions(user),
            UserAuthorizationProjection.GetEffectivePermissions(user));
    }
}
