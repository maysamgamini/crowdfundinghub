using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserCommandHandler : ICommandHandler<AssignRoleToUserCommand, AssignRoleToUserResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;

    public AssignRoleToUserCommandHandler(ICurrentUser currentUser, IUserRepository userRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
    }

    public async Task<AssignRoleToUserResult> Handle(AssignRoleToUserCommand command, CancellationToken cancellationToken)
    {
        EnsureCanAssignRoles();

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        user.AssignRole(command.Role);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return new AssignRoleToUserResult(
            user.Id,
            UserAuthorizationProjection.GetRoles(user),
            UserAuthorizationProjection.GetEffectivePermissions(user));
    }

    private void EnsureCanAssignRoles()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to assign roles.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.IdentityRolesAssign))
        {
            throw new ForbiddenAccessException("The current user does not have permission to assign roles.");
        }
    }
}