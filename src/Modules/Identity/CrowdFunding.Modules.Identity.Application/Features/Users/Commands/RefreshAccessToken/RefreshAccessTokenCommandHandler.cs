using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;

/// <summary>
/// Handles Refresh Access Token command requests: Refresh Token Rotation (RTR) with automated
/// reuse detection (TICKET-036). Every successful call revokes the presented token and issues a
/// brand-new pair — a client is expected to present each refresh token exactly once. Presenting
/// one a second time can only mean either a client bug (rare) or that the token was copied by an
/// attacker and both parties are now racing to use it (the scenario this exists to catch), so the
/// response to either is the same: treat it as a breach and kill every active session for the
/// account rather than trying to distinguish the two.
/// </summary>
public sealed class RefreshAccessTokenCommandHandler : ICommandHandler<RefreshAccessTokenCommand, RefreshAccessTokenResult>
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISecurityStampRevocationStore _revocationStore;
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public RefreshAccessTokenCommandHandler(
        IAccessTokenProvider accessTokenProvider,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        ISecurityStampRevocationStore revocationStore,
        IIdentityDateTimeProvider dateTimeProvider,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _accessTokenProvider = accessTokenProvider;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _revocationStore = revocationStore;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<RefreshAccessTokenResult> Handle(
        RefreshAccessTokenCommand command,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        var tokenHash = _refreshTokenService.Hash(command.RefreshToken);
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (storedToken is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (storedToken.IsRevoked)
        {
            await HandleReuseAsync(storedToken.UserId, now, cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse detected; all sessions have been revoked.");
        }

        if (!storedToken.IsActive(now))
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        var user = await _userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var newRawToken = _refreshTokenService.GenerateToken();
        var newTokenHash = _refreshTokenService.Hash(newRawToken);
        var newRefreshToken = RefreshToken.Issue(user.Id, newTokenHash, now, _refreshTokenService.Lifetime);

        await _transactionExecutor.ExecuteAsync(
            async ct =>
            {
                storedToken.Revoke(now, newTokenHash);
                await _refreshTokenRepository.UpdateAsync(storedToken, ct);
                await _refreshTokenRepository.AddAsync(newRefreshToken, ct);
            },
            cancellationToken);

        var accessToken = _accessTokenProvider.Create(user, UserAuthorizationProjection.GetEffectivePermissions(user));

        return new RefreshAccessTokenResult(accessToken.Value, accessToken.ExpiresAtUtc, newRawToken);
    }

    private async Task HandleReuseAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        await _transactionExecutor.ExecuteAsync(
            async ct =>
            {
                var user = await _userRepository.GetByIdAsync(userId, ct);

                if (user is not null)
                {
                    var compromisedStamp = user.SecurityStamp;
                    user.RotateSecurityStamp();
                    await _userRepository.UpdateAsync(user, ct);
                    await _revocationStore.RevokeAsync(userId, compromisedStamp, ct);
                }

                var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, now, ct);

                foreach (var token in activeTokens)
                {
                    token.Revoke(now);
                    await _refreshTokenRepository.UpdateAsync(token, ct);
                }
            },
            cancellationToken);
    }
}
