using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using CrowdFunding.Modules.Identity.Domain.Aggregates;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

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
            throw new InvalidOperationException($"A user with email '{command.Email}' already exists.");
        }

        var user = User.Register(
            command.Email,
            command.DisplayName,
            _passwordHasher.HashPassword(command.Password),
            _dateTimeProvider.UtcNow);

        if (!await _userRepository.AnyAsync(cancellationToken))
        {
            user.AssignRole(RoleConstants.Admin);
        }
        else
        {
            user.AssignRole(RoleConstants.Creator);
            user.AssignRole(RoleConstants.Backer);
        }

        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id);
    }
}
