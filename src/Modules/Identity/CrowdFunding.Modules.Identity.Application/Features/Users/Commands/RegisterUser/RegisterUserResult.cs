namespace CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;

/// <summary>
/// Represents the outcome returned by Register User.
/// </summary>
public sealed record RegisterUserResult(Guid UserId);
