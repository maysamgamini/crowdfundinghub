using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

/// <summary>
/// Handles Assign Role To User command requests.
/// </summary>
public sealed class AssignRoleToUserCommandHandler : ICommandHandler<AssignRoleToUserCommand, AssignRoleToUserResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public AssignRoleToUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<AssignRoleToUserResult> Handle(AssignRoleToUserCommand command, CancellationToken cancellationToken)
    {
        EnsureCanAssignRoles();

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        user.AssignRole(command.Role);
        await _transactionExecutor.ExecuteAsync(
            ct => _userRepository.UpdateAsync(user, ct),
            cancellationToken);

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
