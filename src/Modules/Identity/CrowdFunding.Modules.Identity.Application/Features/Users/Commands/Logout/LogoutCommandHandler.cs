using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;

/// <summary>
/// Handles Logout command requests: revokes the presented refresh token and rotates the caller's
/// security stamp, publishing the now-stale stamp to the distributed revocation blacklist
/// (TICKET-036) so the currently-active access token — otherwise valid for up to its remaining
/// TTL — is rejected by every module on its very next request.
/// </summary>
public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, LogoutResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISecurityStampRevocationStore _revocationStore;
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public LogoutCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        ISecurityStampRevocationStore revocationStore,
        IIdentityDateTimeProvider dateTimeProvider,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _revocationStore = revocationStore;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<LogoutResult> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to log out.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{_currentUser.UserId}' was not found.");

        var now = _dateTimeProvider.UtcNow;
        var tokenHash = _refreshTokenService.Hash(command.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        await _transactionExecutor.ExecuteAsync(
            async ct =>
            {
                // A token hash that doesn't resolve, or resolves to someone else's token, is
                // silently ignored rather than reported — this is a logout, not a lookup, and
                // must not leak whether an arbitrary token string belongs to another account.
                if (storedToken is not null && storedToken.UserId == user.Id)
                {
                    storedToken.Revoke(now);
                    await _refreshTokenRepository.UpdateAsync(storedToken, ct);
                }

                var staleStamp = user.SecurityStamp;
                user.RotateSecurityStamp();
                await _userRepository.UpdateAsync(user, ct);
                await _revocationStore.RevokeAsync(user.Id, staleStamp, ct);
            },
            cancellationToken);

        return new LogoutResult();
    }
}
