using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

/// <summary>
/// Handles Register User command requests.
/// </summary>
public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IIdentityDateTimeProvider _dateTimeProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRepository _userRepository;

    public RegisterUserCommandHandler(
        IIdentityDateTimeProvider dateTimeProvider,
        IPasswordHasher passwordHasher,
        IUserRepository userRepository)
    {
        _dateTimeProvider = dateTimeProvider;
        _passwordHasher = passwordHasher;
        _userRepository = userRepository;
    }

    public async Task<RegisterUserResult> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmailAddress(command.Email);
        var existingUser = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            // A duplicate email is a resource conflict (RFC 9110 §15.5.10), not a malformed
            // request — mapped to 409 by GlobalExceptionHandler instead of 400, so clients can
            // distinguish "fix your input" from "this account already exists, try logging in".
            throw new ResourceConflictException($"A user with email '{command.Email}' already exists.");
        }

        var user = User.Register(
            command.Email,
            command.DisplayName,
            _passwordHasher.HashPassword(command.Password),
            _dateTimeProvider.UtcNow);

        // Public self-registration always gets standard, non-privileged roles. This previously
        // granted Admin to whichever request won the race to be "the first user" (checked via
        // AnyAsync with no transaction/locking around it) — two concurrent registrations against
        // an empty database could both observe zero users and both become full Administrators.
        // Bootstrap the initial admin out-of-band instead: `dotnet run -- seed-admin` (see
        // AdminSeeder), never through this public, unauthenticated endpoint.
        user.AssignRole(RoleConstants.Creator);
        user.AssignRole(RoleConstants.Backer);

        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id);
    }
}
