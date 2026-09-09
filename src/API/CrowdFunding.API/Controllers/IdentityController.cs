using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.API.RateLimiting;
using CrowdFunding.API.Validation;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.Logout;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RefreshAccessToken;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CrowdFunding.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for Identity: authentication, user registration, role assignment, and permission management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Identity")]
public sealed class IdentityController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IMapper _mapper;
    private readonly IValidator<AssignRoleToUserCommand> _assignRoleValidator;
    private readonly IValidator<GrantPermissionToUserCommand> _grantPermissionValidator;
    private readonly IValidator<LoginUserCommand> _loginValidator;
    private readonly IValidator<RegisterUserCommand> _registerValidator;
    private readonly IValidator<RefreshAccessTokenCommand> _refreshValidator;
    private readonly IValidator<LogoutCommand> _logoutValidator;

    public IdentityController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IMapper mapper,
        IValidator<AssignRoleToUserCommand> assignRoleValidator,
        IValidator<GrantPermissionToUserCommand> grantPermissionValidator,
        IValidator<LoginUserCommand> loginValidator,
        IValidator<RegisterUserCommand> registerValidator,
        IValidator<RefreshAccessTokenCommand> refreshValidator,
        IValidator<LogoutCommand> logoutValidator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _mapper = mapper;
        _assignRoleValidator = assignRoleValidator;
        _grantPermissionValidator = grantPermissionValidator;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
    }

    /// <summary>
    /// Registers a new user account with default Member role.
    /// </summary>
    /// <param name="request">The user registration payload containing email, display name, and password.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="201">User was registered successfully and assigned the default Member role.</response>
    /// <response code="400">Invalid registration data or password complexity failure.</response>
    /// <response code="409">A user with the given email already exists.</response>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<RegisterUserCommand>(request);
        var validationResult = await _registerValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<RegisterUserResult>(command, cancellationToken);

        // Not CreatedAtAction(nameof(Me), ...): /me requires a Bearer token the caller doesn't
        // have yet immediately after registering (registration returns only a UserId, not a
        // token), so a client that follows the Location header per RFC 9110 §10.3.2 would get an
        // unconditional 401. There's no anonymous-accessible single-user resource to point at
        // instead, so this omits Location rather than pointing somewhere guaranteed to fail.
        return StatusCode(StatusCodes.Status201Created, _mapper.Map<RegisterUserResponse>(result));
    }

    /// <summary>
    /// Authenticates a user and returns an asymmetric ES256 JWT access token.
    /// </summary>
    /// <param name="request">The login credentials containing email and password.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Authentication successful. Returns the ES256 JWT access token and expiration timestamp.</response>
    /// <response code="400">Invalid credentials or malformed request payload.</response>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginUserResponse>> Login(
        [FromBody] LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<LoginUserCommand>(request);
        var validationResult = await _loginValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<LoginUserResult>(command, cancellationToken);
        return Ok(_mapper.Map<LoginUserResponse>(result));
    }

    /// <summary>
    /// Exchanges a refresh token for a new access/refresh token pair (Refresh Token Rotation).
    /// The presented refresh token is revoked as part of this call; presenting it again after a
    /// successful refresh is treated as reuse and revokes every active session for the account.
    /// </summary>
    /// <param name="request">The refresh token payload.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns a newly issued access token and refresh token.</response>
    /// <response code="400">Malformed request payload.</response>
    /// <response code="401">The refresh token is invalid, expired, or reuse was detected (all sessions were revoked).</response>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingConfiguration.AuthPolicy)]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefreshAccessTokenResponse>> Refresh(
        [FromBody] RefreshAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<RefreshAccessTokenCommand>(request);
        var validationResult = await _refreshValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<RefreshAccessTokenResult>(command, cancellationToken);
        return Ok(_mapper.Map<RefreshAccessTokenResponse>(result));
    }

    /// <summary>
    /// Logs the current user out: revokes the presented refresh token and rotates the account's
    /// security stamp, so the still-unexpired access token used to call this endpoint is rejected
    /// by every module on its very next request.
    /// </summary>
    /// <param name="request">The refresh token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="204">Logout succeeded; the session has been revoked.</response>
    /// <response code="400">Malformed request payload.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var command = _mapper.Map<LogoutCommand>(request);
        var validationResult = await _logoutValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        await _commandDispatcher.SendAsync<LogoutResult>(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the profile, assigned roles, and effective permissions of the currently authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Returns the authenticated user's ID, email, display name, roles, and effective permissions.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCurrentUserResult>(new GetCurrentUserQuery(), cancellationToken);
        return Ok(_mapper.Map<CurrentUserResponse>(result));
    }

    /// <summary>
    /// Assigns an application role to the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to update.</param>
    /// <param name="request">The role assignment payload containing the role name.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Role assigned successfully. Returns updated roles and permissions.</response>
    /// <response code="400">Invalid role or validation error.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'identity:roles:assign' permission.</response>
    [Authorize(Policy = PermissionConstants.IdentityRolesAssign)]
    [HttpPost("users/{userId:guid}/roles")]
    [ProducesResponseType(typeof(AssignRoleToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AssignRoleToUserResponse>> AssignRole(
        [FromRoute] Guid userId,
        [FromBody] AssignRoleToUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignRoleToUserCommand(userId, request.Role);
        var validationResult = await _assignRoleValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<AssignRoleToUserResult>(command, cancellationToken);
        return Ok(_mapper.Map<AssignRoleToUserResponse>(result));
    }

    /// <summary>
    /// Grants a fine-grained permission directly to the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to update.</param>
    /// <param name="request">The permission grant payload containing the permission name.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous operation.</param>
    /// <response code="200">Permission granted successfully. Returns updated explicit and effective permissions.</response>
    /// <response code="400">Invalid permission or validation error.</response>
    /// <response code="401">Unauthorized if the request lacks a valid Bearer token.</response>
    /// <response code="403">Forbidden if the caller lacks the 'identity:permissions:grant' permission.</response>
    [Authorize(Policy = PermissionConstants.IdentityPermissionsGrant)]
    [HttpPost("users/{userId:guid}/permissions")]
    [ProducesResponseType(typeof(GrantPermissionToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GrantPermissionToUserResponse>> GrantPermission(
        [FromRoute] Guid userId,
        [FromBody] GrantPermissionToUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GrantPermissionToUserCommand(userId, request.Permission);
        var validationResult = await _grantPermissionValidator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var result = await _commandDispatcher.SendAsync<GrantPermissionToUserResult>(command, cancellationToken);
        return Ok(_mapper.Map<GrantPermissionToUserResponse>(result));
    }
}
