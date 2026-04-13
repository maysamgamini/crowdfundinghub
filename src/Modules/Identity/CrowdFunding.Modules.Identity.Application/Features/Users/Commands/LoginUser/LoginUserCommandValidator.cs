using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;

/// <summary>
/// Validates Login User Command instances before they reach the handler.
/// </summary>
public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
