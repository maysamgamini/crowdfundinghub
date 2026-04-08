using CrowdFunding.API.Contracts.Identity;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.AssignRoleToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.GrantPermissionToUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.LoginUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Application.Features.Users.Queries.GetCurrentUser;
using CrowdFunding.Modules.Identity.Contracts.Authorization;
using FluentValidation;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrowdFunding.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IdentityController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IMapper _mapper;
    private readonly IValidator<AssignRoleToUserCommand> _assignRoleValidator;
    private readonly IValidator<GrantPermissionToUserCommand> _grantPermissionValidator;
    private readonly IValidator<LoginUserCommand> _loginValidator;
    private readonly IValidator<RegisterUserCommand> _registerValidator;

    public IdentityController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        IMapper mapper,
        IValidator<AssignRoleToUserCommand> assignRoleValidator,
        IValidator<GrantPermissionToUserCommand> grantPermissionValidator,
        IValidator<LoginUserCommand> loginValidator,
        IValidator<RegisterUserCommand> registerValidator)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _mapper = mapper;
        _assignRoleValidator = assignRoleValidator;
        _grantPermissionValidator = grantPermissionValidator;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
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
        return CreatedAtAction(nameof(Me), _mapper.Map<RegisterUserResponse>(result));
    }

    [AllowAnonymous]
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

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.QueryAsync<GetCurrentUserResult>(new GetCurrentUserQuery(), cancellationToken);
        return Ok(_mapper.Map<CurrentUserResponse>(result));
    }

    [Authorize(Policy = PermissionConstants.IdentityRolesAssign)]
    [HttpPost("users/{userId:guid}/roles")]
    [ProducesResponseType(typeof(AssignRoleToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignRoleToUserResponse>> AssignRole(
        Guid userId,
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

    [Authorize(Policy = PermissionConstants.IdentityPermissionsGrant)]
    [HttpPost("users/{userId:guid}/permissions")]
    [ProducesResponseType(typeof(GrantPermissionToUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GrantPermissionToUserResponse>> GrantPermission(
        Guid userId,
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