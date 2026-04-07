using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

public sealed class GrantPermissionToUserCommandHandler
{
    private readonly IUserRepository _userRepository;

    public GrantPermissionToUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GrantPermissionToUserResult> Handle(
        GrantPermissionToUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        user.GrantPermission(command.Permission);

        await _userRepository.UpdateAsync(user, cancellationToken);

        return new GrantPermissionToUserResult(
            user.Id,
            UserAuthorizationProjection.GetExplicitPermissions(user),
            UserAuthorizationProjection.GetEffectivePermissions(user));
    }
}
