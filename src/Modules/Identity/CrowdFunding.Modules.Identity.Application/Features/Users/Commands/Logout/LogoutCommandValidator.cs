using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;

/// <summary>
/// Validates Logout Command instances before they reach the handler.
/// </summary>
public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}
