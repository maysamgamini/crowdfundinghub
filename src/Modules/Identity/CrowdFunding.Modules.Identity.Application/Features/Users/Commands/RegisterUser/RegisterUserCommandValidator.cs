using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

/// <summary>
/// Validates Register User Command instances before they reach the handler.
/// </summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
