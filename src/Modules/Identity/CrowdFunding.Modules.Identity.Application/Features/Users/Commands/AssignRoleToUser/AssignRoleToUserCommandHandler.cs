using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

public sealed class AssignRoleToUserCommandHandler
{
    private readonly IUserRepository _userRepository;

    public AssignRoleToUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AssignRoleToUserResult> Handle(
        AssignRoleToUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User '{command.UserId}' was not found.");

        user.AssignRole(command.Role);

        await _userRepository.UpdateAsync(user, cancellationToken);

        return new AssignRoleToUserResult(
            user.Id,
            UserAuthorizationProjection.GetRoles(user),
            UserAuthorizationProjection.GetEffectivePermissions(user));
    }
}
