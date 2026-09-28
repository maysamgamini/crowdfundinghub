using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.DeactivateUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrowdFunding.API.Endpoints;

/// <summary>
/// Identity endpoints: registration, login, refresh token rotation, logout, the current user, and
/// administrative role, permission and deactivation operations.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>
    /// Maps the <c>/api/identity</c> group.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group.</returns>
    public static RouteGroupBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        var users = group.MapGroup("/users/{userId:guid}");

        users.MapPost("/roles", AssignRoleAsync)
            .RequireAuthorization(PermissionConstants.IdentityRolesAssign)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/permissions", GrantPermissionAsync)
            .RequireAuthorization(PermissionConstants.IdentityPermissionsGrant)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapPost("/deactivate", DeactivateAsync)
            .RequireAuthorization(PermissionConstants.UsersManage)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Registers a new user account with the default Member role.
    /// </summary>
    /// <remarks>
    /// Returns 201 without a Location header: the only single-user resource, <c>/me</c>, needs a
    /// bearer token the caller does not have yet, so a Location would point somewhere guaranteed
    /// to answer 401 (RFC 9110 §10.3.2).
    /// </remarks>
    /// <param name="request">The e-mail, display name and password.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The new user's id.</returns>
    internal static async Task<Created<RegisterUserResponse>> RegisterAsync(
        RegisterUserRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<RegisterUserResult>(
            new RegisterUserCommand(request.Email, request.DisplayName, request.Password), cancellationToken);

        return TypedResults.Created((string?)null, new RegisterUserResponse(result.UserId));
    }

    /// <summary>
    /// Authenticates a user and issues an ES256 access token and a refresh token.
    /// </summary>
    /// <param name="request">The e-mail and password.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The token pair and the access token's expiry.</returns>
    internal static async Task<Ok<LoginUserResponse>> LoginAsync(
        LoginUserRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<LoginUserResult>(
            new LoginUserCommand(request.Email, request.Password), cancellationToken);

        return TypedResults.Ok(new LoginUserResponse(result.AccessToken, result.ExpiresAtUtc, result.RefreshToken));
    }

    /// <summary>
    /// Exchanges a refresh token for a new token pair (refresh token rotation). Presenting a
    /// rotated token again is treated as theft and revokes every session of the account.
    /// </summary>
    /// <param name="request">The refresh token.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The new token pair.</returns>
    internal static async Task<Ok<RefreshAccessTokenResponse>> RefreshAsync(
        RefreshAccessTokenRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<RefreshAccessTokenResult>(
            new RefreshAccessTokenCommand(request.RefreshToken), cancellationToken);

        return TypedResults.Ok(new RefreshAccessTokenResponse(result.AccessToken, result.ExpiresAtUtc, result.RefreshToken));
    }

    /// <summary>
    /// Logs the current user out: revokes the refresh token and rotates the security stamp, so the
    /// access token used for this call is rejected on its very next use.
    /// </summary>
    /// <param name="request">The refresh token to revoke.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content.</returns>
    internal static async Task<NoContent> LogoutAsync(
        LogoutRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync<LogoutResult>(new LogoutCommand(request.RefreshToken), cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Returns the authenticated user's profile, roles and effective permissions.
    /// </summary>
    /// <param name="dispatcher">The query dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The current user.</returns>
    internal static async Task<Ok<CurrentUserResponse>> GetCurrentUserAsync(
        IQueryDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync<GetCurrentUserResult>(new GetCurrentUserQuery(), cancellationToken);

        return TypedResults.Ok(new CurrentUserResponse(
            result.UserId, result.Email, result.DisplayName, result.Roles, result.ExplicitPermissions, result.Permissions));
    }

    /// <summary>
    /// Assigns an application role to a user.
    /// </summary>
    /// <param name="userId">The user to update.</param>
    /// <param name="request">The role name.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The user's roles and effective permissions.</returns>
    internal static async Task<Ok<AssignRoleToUserResponse>> AssignRoleAsync(
        Guid userId, AssignRoleToUserRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<AssignRoleToUserResult>(
            new AssignRoleToUserCommand(userId, request.Role), cancellationToken);

        return TypedResults.Ok(new AssignRoleToUserResponse(result.UserId, result.Roles, result.Permissions));
    }

    /// <summary>
    /// Grants a fine-grained permission directly to a user.
    /// </summary>
    /// <param name="userId">The user to update.</param>
    /// <param name="request">The permission name.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The user's explicit and effective permissions.</returns>
    internal static async Task<Ok<GrantPermissionToUserResponse>> GrantPermissionAsync(
        Guid userId, GrantPermissionToUserRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<GrantPermissionToUserResult>(
            new GrantPermissionToUserCommand(userId, request.Permission), cancellationToken);

        return TypedResults.Ok(new GrantPermissionToUserResponse(result.UserId, result.ExplicitPermissions, result.Permissions));
    }

    /// <summary>
    /// Deactivates a user account and immediately revokes every access and refresh token issued to it.
    /// </summary>
    /// <param name="userId">The user to deactivate.</param>
    /// <param name="dispatcher">The command dispatcher.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The user's id and active flag.</returns>
    internal static async Task<Ok<DeactivateUserResponse>> DeactivateAsync(
        Guid userId, ICommandDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync<DeactivateUserResult>(new DeactivateUserCommand(userId), cancellationToken);
        return TypedResults.Ok(new DeactivateUserResponse(result.UserId, result.IsActive));
    }
}
