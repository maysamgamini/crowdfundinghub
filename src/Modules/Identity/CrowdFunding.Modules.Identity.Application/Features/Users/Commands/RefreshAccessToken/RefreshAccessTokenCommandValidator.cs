using FluentValidation;

namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;

/// <summary>
/// Validates Refresh Access Token Command instances before they reach the handler.
/// </summary>
public sealed class RefreshAccessTokenCommandValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}
