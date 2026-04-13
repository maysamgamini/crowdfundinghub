using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

/// <summary>
/// Handles Login User command requests.
/// </summary>
public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, LoginUserResult>
{
    private readonly IAccessTokenProvider _accessTokenProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;

    public LoginUserCommandHandler(
        IAccessTokenProvider accessTokenProvider,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository)
    {
        _accessTokenProvider = accessTokenProvider;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
    }

    public async Task<LoginUserResult> Handle(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmailAddress(command.Email);
        var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(user.PasswordHash, command.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("The user account is inactive.");
        }

        var accessToken = _accessTokenProvider.Create(user, UserAuthorizationProjection.GetEffectivePermissions(user));

        return new LoginUserResult(accessToken.Value, accessToken.ExpiresAtUtc);
    }
}
