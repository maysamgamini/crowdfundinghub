using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.SeedAdmin;

/// <summary>
/// Handles Seed Admin command requests. See SeedAdminCommand's remarks: this is the only place
/// in the codebase permitted to grant RoleConstants.Admin, and it's reachable only from the CLI.
/// </summary>
public sealed class SeedAdminCommandHandler : ICommandHandler<SeedAdminCommand, SeedAdminResult>
{
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;

    public SeedAdminCommandHandler(
        IIdentityDateTimeProvider dateTimeProvider,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
    }

    public async Task<SeedAdminResult> Handle(SeedAdminCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmailAddress(command.Email);
        var existingUser = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Roles.Any(role => role.Role == RoleConstants.Admin))
            {
                return new SeedAdminResult(existingUser.Id, WasNewlyCreated: false);
            }

            existingUser.AssignRole(RoleConstants.Admin);
            await _userRepository.UpdateAsync(existingUser, cancellationToken);
            return new SeedAdminResult(existingUser.Id, WasNewlyCreated: false);
        }

        var user = User.Register(
            command.Email,
            command.DisplayName,
            _passwordHasher.HashPassword(command.Password),
            _dateTimeProvider.UtcNow);
        user.AssignRole(RoleConstants.Admin);

        await _userRepository.AddAsync(user, cancellationToken);

        return new SeedAdminResult(user.Id, WasNewlyCreated: true);
    }
}
