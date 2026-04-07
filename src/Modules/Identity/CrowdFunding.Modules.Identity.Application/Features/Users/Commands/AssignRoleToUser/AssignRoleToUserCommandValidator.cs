using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;

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
