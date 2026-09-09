using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Domain.Entities;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

/// <summary>
/// Handles Login User command requests.
/// </summary>
public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, LoginUserResult>
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IIdentityTransactionExecutor _transactionExecutor;

    public LoginUserCommandHandler(
        IAccessTokenProvider accessTokenProvider,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        IIdentityDateTimeProvider dateTimeProvider,
        IIdentityTransactionExecutor transactionExecutor)
    {
        _accessTokenProvider = accessTokenProvider;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _dateTimeProvider = dateTimeProvider;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<LoginUserResult> Handle(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmailAddress(command.Email);
        var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        // Constant-time login (improvement.md §2.8): always run PBKDF2, against the real hash
        // when the user exists or a fixed DummyHash when they don't, so response latency can't
        // be used to enumerate registered email addresses. The inactive-account case is folded
        // into the same generic exception instead of its own message, which previously confirmed
        // the account exists even when the credentials check would otherwise have failed.
        var hashToVerify = user?.PasswordHash ?? _passwordHasher.DummyHash;
        var passwordValid = _passwordHasher.VerifyPassword(hashToVerify, command.Password);

        if (user is null || !passwordValid || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var accessToken = _accessTokenProvider.Create(user, UserAuthorizationProjection.GetEffectivePermissions(user));

        var rawRefreshToken = _refreshTokenService.GenerateToken();
        var refreshToken = RefreshToken.Issue(
            user.Id,
            _refreshTokenService.Hash(rawRefreshToken),
            _dateTimeProvider.UtcNow,
            _refreshTokenService.Lifetime);

        await _transactionExecutor.ExecuteAsync(
            ct => _refreshTokenRepository.AddAsync(refreshToken, ct),
            cancellationToken);

        return new LoginUserResult(accessToken.Value, accessToken.ExpiresAtUtc, rawRefreshToken);
    }
}
