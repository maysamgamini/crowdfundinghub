using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.DeactivateUser;

/// <summary>
/// Validates Deactivate User Command instances before they reach the handler.
/// </summary>
public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
