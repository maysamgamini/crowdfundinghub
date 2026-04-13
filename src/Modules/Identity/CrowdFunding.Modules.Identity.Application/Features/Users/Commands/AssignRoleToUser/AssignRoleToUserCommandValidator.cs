using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

/// <summary>
/// Validates Assign Role To User Command instances before they reach the handler.
/// </summary>
public sealed class AssignRoleToUserCommandValidator : AbstractValidator<AssignRoleToUserCommand>
{
    public AssignRoleToUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => RoleConstants.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", RoleConstants.All)}.");
    }
}
