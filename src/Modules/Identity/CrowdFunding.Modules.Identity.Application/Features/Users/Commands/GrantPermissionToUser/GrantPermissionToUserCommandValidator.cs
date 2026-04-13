using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;

/// <summary>
/// Validates Grant Permission To User Command instances before they reach the handler.
/// </summary>
public sealed class GrantPermissionToUserCommandValidator : AbstractValidator<GrantPermissionToUserCommand>
{
    public GrantPermissionToUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Permission)
            .NotEmpty()
            .Must(permission => PermissionConstants.All.Contains(permission, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Permission must be one of: {string.Join(", ", PermissionConstants.All)}.");
    }
}
