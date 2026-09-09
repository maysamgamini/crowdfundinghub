using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Contracts.Authorization;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.DeactivateUser;

/// <summary>
/// Handles Deactivate User command requests: the administrative counterpart to
/// <c>RefreshAccessTokenCommandHandler</c>'s automatic reuse-detection revocation — an
/// administrator suspending a fraudulent or compromised account gets the exact same instant,
/// decentralized session kill: the pre-deactivation security stamp is published to the
/// distributed revocation blacklist (rejecting every already-issued access token immediately,
/// not just after natural expiry) and every active refresh token is revoked so the account
/// cannot be silently refreshed back into a live session. See TICKET-049.
/// </summary>
public sealed class DeactivateUserCommandHandler : ICommandHandler<DeactivateUserCommand, DeactivateUserResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ISecurityStampRevocationStore _revocationStore;
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public DeactivateUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ISecurityStampRevocationStore revocationStore,
        IIdentityDateTimeProvider dateTimeProvider,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _revocationStore = revocationStore;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<DeactivateUserResult> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        EnsureCanManageUsers();

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        var compromisedStamp = user.SecurityStamp;
        var now = _dateTimeProvider.UtcNow;

        user.Deactivate();

        await _transactionExecutor.ExecuteAsync(
            async ct =>
            {
                await _userRepository.UpdateAsync(user, ct);
                await _revocationStore.RevokeAsync(user.Id, compromisedStamp, ct);

                var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(user.Id, now, ct);

                foreach (var token in activeTokens)
                {
                    token.Revoke(now);
                    await _refreshTokenRepository.UpdateAsync(token, ct);
                }
            },
            cancellationToken);

        return new DeactivateUserResult(user.Id, user.IsActive);
    }

    private void EnsureCanManageUsers()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to deactivate a user.");
        }

        if (!_currentUser.HasPermission(PermissionConstants.UsersManage))
        {
            throw new ForbiddenAccessException("The current user does not have permission to deactivate users.");
        }
    }
}
