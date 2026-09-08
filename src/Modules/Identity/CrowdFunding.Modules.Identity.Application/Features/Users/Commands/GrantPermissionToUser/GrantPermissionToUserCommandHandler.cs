using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

/// <summary>
/// Handles Grant Permission To User command requests.
/// </summary>
public sealed class GrantPermissionToUserCommandHandler : ICommandHandler<GrantPermissionToUserCommand, GrantPermissionToUserResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public GrantPermissionToUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task<GrantPermissionToUserResult> Handle(GrantPermissionToUserCommand command, CancellationToken cancellationToken)
    {
        EnsureCanGrantPermissions();

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        user.GrantPermission(command.Permission);
        await _transactionExecutor.ExecuteAsync(
            ct => _userRepository.UpdateAsync(user, ct),
            cancellationToken);

        return new GrantPermissionToUserResult(
            user.Id,
            UserAuthorizationProjection.GetExplicitPermissions(user),
            UserAuthorizationProjection.GetEffectivePermissions(user));
    }

    private void EnsureCanGrantPermissions()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to grant permissions.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.IdentityPermissionsGrant))
        {
            throw new ForbiddenAccessException("The current user does not have permission to grant permissions.");
        }
    }
}
